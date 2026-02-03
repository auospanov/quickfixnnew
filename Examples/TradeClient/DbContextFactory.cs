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
            
            // Убираем двойные точки с запятой и лишние пробелы
            cleaned = cleaned.Replace(";;", ";").TrimEnd(';', ' ');

            return cleaned;
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
                        if (_sharedConnection.State != System.Data.ConnectionState.Closed)
                        {
                            _sharedConnection.Close();
                        }
                        _sharedConnection.Dispose();
                    }
                    catch { }
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
