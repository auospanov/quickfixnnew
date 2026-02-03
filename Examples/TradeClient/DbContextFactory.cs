using Microsoft.EntityFrameworkCore;
using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace TradeClient
{
    /// <summary>
    /// Обертка для DbContext, которая игнорирует Dispose() для использования в using
    /// Обеспечивает потокобезопасный доступ к контексту
    /// </summary>
    public class DisposableDbContextWrapper : IDisposable
    {
        private readonly MyDbContext _context;
        private readonly SemaphoreSlim _semaphore;
        private bool _semaphoreAcquired = false;
        private const int SemaphoreTimeoutMs = 30000; // 30 секунд таймаут

        public DisposableDbContextWrapper(MyDbContext context, SemaphoreSlim semaphore)
        {
            _context = context;
            _semaphore = semaphore;
            
            // Логируем попытку захвата семафора
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var stackTrace = new StackTrace(1, true);
            Debug.WriteLine($"[DbContext] Поток {threadId} пытается захватить семафор. Текущее количество: {semaphore.CurrentCount}");
            
            var startTime = DateTime.Now;
            // Захватываем семафор при создании обертки с таймаутом
            _semaphoreAcquired = _semaphore.Wait(SemaphoreTimeoutMs);
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            
            if (!_semaphoreAcquired)
            {
                Debug.WriteLine($"[DbContext] Поток {threadId} НЕ смог захватить семафор за {elapsed} мс. StackTrace: {stackTrace}");
                throw new TimeoutException($"Не удалось получить доступ к DbContext в течение {SemaphoreTimeoutMs} мс. Возможно, другой поток удерживает блокировку слишком долго. Поток: {threadId}");
            }
            
            Debug.WriteLine($"[DbContext] Поток {threadId} успешно захватил семафор за {elapsed} мс");
        }

        public MyDbContext Context => _context;

        public void Dispose()
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            
            // ВАЖНО: Освобождаем семафор ПЕРВЫМ ДЕЛОМ, чтобы другие потоки не ждали
            // Очистка ChangeTracker может быть медленной, поэтому делаем её после освобождения семафора
            if (_semaphoreAcquired)
            {
                try
                {
                    _semaphore.Release();
                    Debug.WriteLine($"[DbContext] Поток {threadId} освободил семафор. Текущее количество: {_semaphore.CurrentCount}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DbContext] Ошибка при освобождении семафора в потоке {threadId}: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"[DbContext] Поток {threadId} не освобождает семафор, т.к. не захватывал его");
            }
            
            // Теперь очищаем трекер изменений БЕЗ блокировки семафора
            // Это безопасно, т.к. мы уже освободили семафор
            try
            {
                if (_context != null)
                {
                    // Проверяем, что Database доступен (модель создана) перед использованием ChangeTracker
                    var database = _context.Database;
                    if (database != null)
                    {
                        try
                        {
                            _context.ChangeTracker.Clear();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[DbContext] Ошибка при очистке ChangeTracker в потоке {threadId}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DbContext] Ошибка в Dispose потока {threadId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Фабрика для создания экземпляров DbContext с единым долгоживущим подключением
    /// Использует один DbContext, который не закрывается
    /// </summary>
    public class DbContextFactory
    {
        private readonly MyDbContext _sharedDbContext;
        private readonly SqlConnection _sharedConnection; // Явное управление соединением
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // Только один поток может использовать контекст одновременно

        private static DbContextFactory? _instance;
        private static readonly object _instanceLock = new object();

        /// <summary>
        /// Получить единственный экземпляр фабрики (Singleton)
        /// </summary>
        public static DbContextFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            throw new InvalidOperationException("DbContextFactory не инициализирована. Вызовите Initialize() перед использованием.");
                        }
                    }
                }
                return _instance;
            }
        }

        private DbContextFactory(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            
            // Очищаем строку подключения от неподдерживаемых параметров
            var cleanedConnectionString = CleanConnectionString(connectionString);

            // Создаем одно долгоживущее соединение и открываем его один раз
            _sharedConnection = new SqlConnection(cleanedConnectionString);
            _sharedConnection.Open(); // Открываем соединение один раз при инициализации

            // Создаем DbContext с уже открытым соединением
            // Это гарантирует, что соединение не будет закрываться при Dispose()
            var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();
            optionsBuilder.UseSqlServer(_sharedConnection, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            _sharedDbContext = new MyDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// Очищает строку подключения от параметров, не поддерживаемых System.Data.SqlClient
        /// </summary>
        private string CleanConnectionString(string connectionString)
        {
            // System.Data.SqlClient не поддерживает "Trust Server Certificate" (с пробелами)
            // Заменяем на "TrustServerCertificate" (без пробелов)
            var cleaned = connectionString;
            
            // Заменяем "Trust Server Certificate" на "TrustServerCertificate"
            if (cleaned.Contains("Trust Server Certificate", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = Regex.Replace(
                    cleaned, 
                    @"Trust\s+Server\s+Certificate\s*=\s*([^;]+)", 
                    "TrustServerCertificate=$1", 
                    RegexOptions.IgnoreCase);
            }
            
            // Убираем двойные точки с запятой и лишние пробелы
            cleaned = cleaned.Replace(";;", ";").TrimEnd(';', ' ');

            return cleaned;
        }

        /// <summary>
        /// Инициализация фабрики с строкой подключения
        /// </summary>
        public static void Initialize(string connectionString)
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = new DbContextFactory(connectionString);
                    }
                }
            }
        }

        /// <summary>
        /// Возвращает обертку для единого долгоживущего DbContext
        /// Можно использовать в using - Dispose() не закроет подключение
        /// Обеспечивает потокобезопасный доступ к контексту
        /// </summary>
        public DisposableDbContextWrapper CreateDbContext()
        {
            // Возвращаем обертку вокруг одного и того же контекста для всех операций
            // Подключение остается открытым на протяжении жизни приложения
            // Семафор гарантирует, что только один поток может использовать контекст одновременно
            return new DisposableDbContextWrapper(_sharedDbContext, _semaphore);
        }

        /// <summary>
        /// Освобождает ресурсы фабрики и закрывает подключение
        /// </summary>
        public static void Dispose()
        {
            if (_instance != null)
            {
                lock (_instanceLock)
                {
                    if (_instance != null)
                    {
                        try
                        {
                            // Ждем, пока все операции с контекстом завершатся
                            _instance._semaphore.Wait();
                            try
                            {
                                // Закрываем долгоживущий контекст
                                _instance._sharedDbContext?.Dispose();
                                // Закрываем долгоживущее соединение
                                _instance._sharedConnection?.Close();
                                _instance._sharedConnection?.Dispose();
                            }
                            finally
                            {
                                _instance._semaphore.Release();
                            }
                            _instance._semaphore.Dispose();
                        }
                        catch { }
                        _instance = null;
                    }
                }
            }
        }
    }
}
