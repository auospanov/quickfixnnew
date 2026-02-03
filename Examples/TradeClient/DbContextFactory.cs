using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace TradeClient
{
    /// <summary>
    /// Обертка для DbContext из пула, которая управляет IServiceScope
    /// </summary>
    public class PooledDbContextWrapper : IDisposable
    {
        private readonly IServiceScope _scope;
        public MyDbContext Context { get; }

        public PooledDbContextWrapper(IServiceScope scope, MyDbContext context)
        {
            _scope = scope;
            Context = context;
        }

        public void Dispose()
        {
            // Сначала освобождаем контекст (он вернется в пул)
            // Затем освобождаем scope
            _scope?.Dispose();
        }
    }

    /// <summary>
    /// Фабрика для создания экземпляров DbContext с использованием пула контекстов
    /// Использует AddDbContextPool для эффективного управления контекстами и соединениями
    /// </summary>
    public class DbContextFactory
    {
        private static IServiceProvider? _serviceProvider;
        private static readonly object _initLock = new object();

        /// <summary>
        /// Инициализация фабрики с строкой подключения
        /// </summary>
        public static void Initialize(string connectionString)
        {
            if (_serviceProvider == null)
            {
                lock (_initLock)
                {
                    if (_serviceProvider == null)
                    {
                        // Очищаем строку подключения от неподдерживаемых параметров
                        var cleanedConnectionString = CleanConnectionString(connectionString);

                        // Создаем ServiceCollection и настраиваем пул контекстов
                        var services = new ServiceCollection();
                        
                        // AddDbContextPool - правильный способ для многопоточных приложений
                        // Он создает пул контекстов и правильно управляет соединениями
                        // При использовании через IServiceScope контексты берутся из пула и переиспользуются
                        services.AddDbContextPool<MyDbContext>(options =>
                        {
                            options.UseSqlServer(cleanedConnectionString, sqlOptions =>
                            {
                                sqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: 3,
                                    maxRetryDelay: TimeSpan.FromSeconds(30),
                                    errorNumbersToAdd: null);
                            });
                        }, poolSize: 128); // Размер пула контекстов

                        _serviceProvider = services.BuildServiceProvider();
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
                if (_serviceProvider == null)
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
        /// Создает новый экземпляр DbContext из пула через IServiceScope
        /// Каждый вызов возвращает обертку с контекстом из пула, который можно безопасно использовать в одном потоке
        /// После Dispose() контекст возвращается в пул, а соединение переиспользуется через ADO.NET connection pooling
        /// </summary>
        public PooledDbContextWrapper CreateDbContext()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("DbContextFactory не инициализирована.");
            }
            
            // Создаем scope для получения контекста из пула
            // Это критически важно - без scope контексты не будут браться из пула AddDbContextPool
            var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            return new PooledDbContextWrapper(scope, context);
        }

        /// <summary>
        /// Освобождает ресурсы фабрики
        /// </summary>
        public static void Dispose()
        {
            if (_serviceProvider != null)
            {
                lock (_initLock)
                {
                    if (_serviceProvider != null)
                    {
                        try
                        {
                            if (_serviceProvider is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                        }
                        catch { }
                        _serviceProvider = null;
                    }
                }
            }
        }
    }
}
