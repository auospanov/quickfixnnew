using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace TradeClient
{
    /// <summary>
    /// Фабрика для создания экземпляров DbContext с использованием пула контекстов
    /// Использует AddDbContextPool для эффективного управления контекстами и соединениями
    /// </summary>
    public class DbContextFactory
    {
        private static IServiceProvider? _serviceProvider;
        private static IDbContextFactory<MyDbContext>? _dbContextFactory;
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

                        // Создаем фабрику контекстов для явного создания экземпляров
                        services.AddDbContextFactory<MyDbContext>(options =>
                        {
                            options.UseSqlServer(cleanedConnectionString, sqlOptions =>
                            {
                                sqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: 3,
                                    maxRetryDelay: TimeSpan.FromSeconds(30),
                                    errorNumbersToAdd: null);
                            });
                        });

                        _serviceProvider = services.BuildServiceProvider();
                        _dbContextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<MyDbContext>>();
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
                if (_dbContextFactory == null)
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
        /// Создает новый экземпляр DbContext из пула
        /// Каждый вызов возвращает новый контекст, который можно безопасно использовать в одном потоке
        /// После Dispose() контекст возвращается в пул, а соединение переиспользуется
        /// </summary>
        public MyDbContext CreateDbContext()
        {
            if (_dbContextFactory == null)
            {
                throw new InvalidOperationException("DbContextFactory не инициализирована.");
            }
            
            // Создаем новый контекст из пула
            // Это потокобезопасно - каждый поток получает свой контекст
            return _dbContextFactory.CreateDbContext();
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
                        _dbContextFactory = null;
                    }
                }
            }
        }
    }
}
