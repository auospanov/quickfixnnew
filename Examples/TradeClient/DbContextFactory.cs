using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace TradeClient
{
    /// <summary>
    /// Фабрика для создания экземпляров DbContext с использованием одного общего подключения
    /// Использует одно физическое подключение к БД для предотвращения множественных login/logout
    /// </summary>
    public class DbContextFactory : IDisposable
    {
        private static MyDbContext? _sharedContext;
        private static SqlConnection? _sharedConnection;
        private static readonly object _initLock = new object();
        private static readonly object _contextLock = new object();

        /// <summary>
        /// Инициализация фабрики с строкой подключения
        /// </summary>
        public static void Initialize(string connectionString)
        {
            if (_sharedContext == null)
            {
                lock (_initLock)
                {
                    if (_sharedContext == null)
                    {
                        // Очищаем строку подключения от неподдерживаемых параметров
                        var cleanedConnectionString = CleanConnectionString(connectionString);

                        // Создаем одно физическое подключение, которое будет переиспользоваться
                        // Это критически важно для предотвращения множественных login/logout событий
                        _sharedConnection = new SqlConnection(cleanedConnectionString);
                        _sharedConnection.Open(); // Открываем подключение один раз при инициализации
                        
                        // Логируем успешное подключение для отслеживания
                        Console.WriteLine($"[DbContextFactory] Подключение к БД открыто. ConnectionId: {_sharedConnection.ClientConnectionId}, State: {_sharedConnection.State}");

                        // Создаем один общий контекст, который использует это открытое подключение
                        var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();
                        optionsBuilder.UseSqlServer(_sharedConnection, sqlOptions =>
                        {
                            sqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 3,
                                maxRetryDelay: TimeSpan.FromSeconds(30),
                                errorNumbersToAdd: null);
                        });

                        _sharedContext = new MyDbContext(optionsBuilder.Options);
                    }
                }
            }
        }

        /// <summary>
        /// Очищает строку подключения от параметров, не поддерживаемых System.Data.SqlClient
        /// И отключает пул подключений для использования строго одного подключения
        /// </summary>
        private static string CleanConnectionString(string connectionString)
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
            
            // КРИТИЧЕСКИ ВАЖНО: Отключаем пул подключений ADO.NET
            // Это гарантирует использование строго одного подключения
            if (cleaned.Contains("Pooling", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = Regex.Replace(
                    cleaned,
                    @"Pooling\s*=\s*[^;]+",
                    "Pooling=false",
                    RegexOptions.IgnoreCase);
            }
            else
            {
                cleaned = cleaned.TrimEnd(';', ' ') + ";Pooling=false";
            }
            
            // ВАЖНО: Включаем MARS (Multiple Active Result Sets) для поддержки нескольких активных запросов
            // Это необходимо, так как одно подключение используется и для EF Core, и для прямых ADO.NET запросов
            if (!cleaned.Contains("MultipleActiveResultSets", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.TrimEnd(';', ' ') + ";MultipleActiveResultSets=True";
            }
            
            // Удаляем параметры пула, которые могут создавать дополнительные подключения
            cleaned = Regex.Replace(cleaned, @"Max\s+Pool\s+Size\s*=\s*[^;]+;?", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"Min\s+Pool\s+Size\s*=\s*[^;]+;?", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"Connection\s+Lifetime\s*=\s*[^;]+;?", "", RegexOptions.IgnoreCase);
            
            // Убираем двойные точки с запятой и лишние пробелы
            cleaned = cleaned.Replace(";;", ";").TrimEnd(';', ' ');
            
            return cleaned;
        }

        /// <summary>
        /// Получить общее подключение для использования в других методах (например, isStop)
        /// </summary>
        public static SqlConnection? GetSharedConnection()
        {
            return _sharedConnection;
        }

        /// <summary>
        /// Получить единственный экземпляр фабрики (Singleton)
        /// </summary>
        public static DbContextFactory Instance
        {
            get
            {
                if (_sharedContext == null)
                {
                    throw new InvalidOperationException("DbContextFactory не инициализирована. Вызовите Initialize() перед использованием.");
                }
                return new DbContextFactory();
            }
        }

        private DbContextFactory()
        {
            // Приватный конструктор для Singleton
        }

        /// <summary>
        /// Возвращает обертку для безопасного использования общего контекста
        /// Использует lock для синхронизации доступа к контексту
        /// </summary>
        public DisposableDbContextWrapper CreateDbContext()
        {
            if (_sharedContext == null)
            {
                throw new InvalidOperationException("DbContextFactory не инициализирована.");
            }
            
            return new DisposableDbContextWrapper(_sharedContext, _contextLock);
        }

        /// <summary>
        /// Освобождает текущий контекст (очищает ChangeTracker)
        /// </summary>
        public void Dispose()
        {
            // Не освобождаем общий контекст, только очищаем трекер изменений
        }

        /// <summary>
        /// Освобождает ресурсы фабрики (статический метод)
        /// </summary>
        public static void DisposeStatic()
        {
            lock (_initLock)
            {
                if (_sharedContext != null)
                {
                    try
                    {
                        _sharedContext.Dispose();
                    }
                    catch { }
                    _sharedContext = null;
                }
                
                if (_sharedConnection != null)
                {
                    try
                    {
                        Console.WriteLine($"[DbContextFactory] Закрытие подключения к БД. ConnectionId: {_sharedConnection.ClientConnectionId}, State: {_sharedConnection.State}");
                        if (_sharedConnection.State != System.Data.ConnectionState.Closed)
                        {
                            _sharedConnection.Close();
                        }
                        _sharedConnection.Dispose();
                        Console.WriteLine("[DbContextFactory] Подключение к БД закрыто и освобождено");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DbContextFactory] Ошибка при закрытии подключения: {ex.Message}");
                    }
                    _sharedConnection = null;
                }
            }
        }
    }

    /// <summary>
    /// Обертка для безопасного использования общего DbContext с синхронизацией
    /// </summary>
    public class DisposableDbContextWrapper : IDisposable
    {
        private readonly MyDbContext _context;
        private readonly object _lock;
        private bool _lockTaken = false;

        public DisposableDbContextWrapper(MyDbContext context, object lockObject)
        {
            _context = context;
            _lock = lockObject;
            System.Threading.Monitor.Enter(_lock, ref _lockTaken);
        }

        public MyDbContext Context => _context;

        public void Dispose()
        {
            try
            {
                // Очищаем трекер изменений перед освобождением блокировки
                _context.ChangeTracker.Clear();
            }
            catch { }
            finally
            {
                if (_lockTaken)
                {
                    System.Threading.Monitor.Exit(_lock);
                    _lockTaken = false;
                }
            }
        }
    }
}
