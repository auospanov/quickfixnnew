using Microsoft.EntityFrameworkCore;
using System;

namespace TradeClient
{
    /// <summary>
    /// Фабрика для создания экземпляров DbContext с единым пулом подключений
    /// Использует переиспользование опций и пул подключений ADO.NET
    /// </summary>
    public class DbContextFactory
    {
        private readonly DbContextOptions<MyDbContext> _options;

        private static DbContextFactory? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Получить единственный экземпляр фабрики (Singleton)
        /// </summary>
        public static DbContextFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
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
            
            // Настраиваем опции для DbContext один раз и переиспользуем их
            // Пул подключений ADO.NET настроен в строке подключения (Min Pool Size, Max Pool Size)
            // Это обеспечивает переиспользование физических подключений к БД
            var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();
            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            _options = optionsBuilder.Options;
        }

        /// <summary>
        /// Инициализация фабрики с строкой подключения
        /// </summary>
        public static void Initialize(string connectionString)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new DbContextFactory(connectionString);
                    }
                }
            }
        }

        /// <summary>
        /// Создает новый экземпляр DbContext
        /// Подключения к БД переиспользуются через пул ADO.NET (настроен в строке подключения)
        /// </summary>
        public MyDbContext CreateDbContext()
        {
            return new MyDbContext(_options);
        }

        /// <summary>
        /// Освобождает ресурсы фабрики
        /// </summary>
        public static void Dispose()
        {
            if (_instance != null)
            {
                lock (_lock)
                {
                    _instance = null;
                }
            }
        }
    }
}
