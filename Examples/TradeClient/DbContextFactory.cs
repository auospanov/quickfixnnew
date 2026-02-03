using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace TradeClient
{
    /// <summary>
    /// Фабрика для создания экземпляров DbContext с единым пулом подключений
    /// Использует AddDbContextPool для реального пула подключений Entity Framework
    /// </summary>
    public class DbContextFactory
    {
        private readonly string _connectionString;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDbContextFactory<MyDbContext> _factory;

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
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            
            // Создаем ServiceProvider с пулом DbContext
            // AddDbContextPool переиспользует экземпляры DbContext и их подключения к БД
            // Это означает, что подключения НЕ закрываются при Dispose(), а возвращаются в пул
            _serviceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .AddDbContextPool<MyDbContext>(options => 
                {
                    options.UseSqlServer(_connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
                }, poolSize: 128) // Размер пула DbContext (стандартное значение)
                .BuildServiceProvider();

            // Получаем фабрику из ServiceProvider
            // При использовании AddDbContextPool, фабрика автоматически использует пул
            _factory = _serviceProvider.GetRequiredService<IDbContextFactory<MyDbContext>>();
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
        /// Создает или получает экземпляр DbContext из пула
        /// При использовании AddDbContextPool, Dispose() возвращает контекст в пул,
        /// а не закрывает подключение к БД
        /// </summary>
        public MyDbContext CreateDbContext()
        {
            return _factory.CreateDbContext();
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
                    if (_instance?._serviceProvider is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _instance = null;
                }
            }
        }
    }
}
