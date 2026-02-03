using Microsoft.EntityFrameworkCore;
using System;
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

        public DisposableDbContextWrapper(MyDbContext context, SemaphoreSlim semaphore)
        {
            _context = context;
            _semaphore = semaphore;
            // Захватываем семафор при создании обертки
            _semaphore.Wait();
        }

        public MyDbContext Context => _context;

        public void Dispose()
        {
            // НЕ вызываем Dispose() на контексте - просто очищаем трекер изменений
            // Обертываем в try-catch, чтобы избежать ошибок, если модель еще создается
            try
            {
                if (_context != null)
                {
                    // Проверяем, что Database доступен (модель создана) перед использованием ChangeTracker
                    var database = _context.Database;
                    if (database != null)
                    {
                        _context.ChangeTracker.Clear();
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки при очистке трекера (например, если модель еще создается)
            }
            finally
            {
                // Освобождаем семафор, чтобы другие потоки могли использовать контекст
                _semaphore.Release();
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

            // Создаем один долгоживущий DbContext с долгоживущим подключением
            var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();
            optionsBuilder.UseSqlServer(cleanedConnectionString, sqlOptions =>
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
                                // Закрываем долгоживущий контекст и его подключение
                                _instance._sharedDbContext?.Dispose();
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
