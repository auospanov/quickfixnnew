using Microsoft.EntityFrameworkCore;
using System;
using System.Text.RegularExpressions;

namespace TradeClient
{
    /// <summary>
    /// Обертка для DbContext, которая игнорирует Dispose() для использования в using
    /// </summary>
    public class DisposableDbContextWrapper : IDisposable
    {
        private readonly MyDbContext _context;

        public DisposableDbContextWrapper(MyDbContext context)
        {
            _context = context;
        }

        public MyDbContext Context => _context;

        public void Dispose()
        {
            // НЕ вызываем Dispose() на контексте - просто очищаем трекер изменений
            _context.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Фабрика для создания экземпляров DbContext с единым долгоживущим подключением
    /// Использует один DbContext, который не закрывается
    /// </summary>
    public class DbContextFactory
    {
        private readonly MyDbContext _sharedDbContext;
        private readonly object _lock = new object();

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
        /// </summary>
        public DisposableDbContextWrapper CreateDbContext()
        {
            // Возвращаем обертку вокруг одного и того же контекста для всех операций
            // Подключение остается открытым на протяжении жизни приложения
            return new DisposableDbContextWrapper(_sharedDbContext);
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
                            // Закрываем долгоживущий контекст и его подключение
                            _instance._sharedDbContext?.Dispose();
                        }
                        catch { }
                        _instance = null;
                    }
                }
            }
        }
    }
}
