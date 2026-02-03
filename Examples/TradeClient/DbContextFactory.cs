using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Data.SqlClient;

namespace TradeClient
{
    /// <summary>
    /// Обертка для DbContext, которая не закрывает подключение при Dispose()
    /// </summary>
    public class PooledDbContext : MyDbContext
    {
        private readonly SqlConnection _connection;
        private bool _disposed = false;

        public PooledDbContext(DbContextOptions<MyDbContext> options, SqlConnection connection) : base(options)
        {
            _connection = connection;
        }

        public new void Dispose()
        {
            if (!_disposed)
            {
                // Очищаем трекер изменений
                ChangeTracker.Clear();
                
                // Возвращаем подключение в пул вместо закрытия
                DbContextFactory.Instance.ReturnConnection(_connection);
                
                _disposed = true;
            }
            // НЕ вызываем base.Dispose() чтобы не закрывать подключение
        }
    }

    /// <summary>
    /// Фабрика для создания экземпляров DbContext с пулом долгоживущих подключений
    /// Использует пул подключений, которые не закрываются при Dispose() DbContext
    /// </summary>
    public class DbContextFactory
    {
        private readonly string _connectionString;
        private readonly ConcurrentQueue<SqlConnection> _connectionPool;
        private readonly int _poolSize = 5; // Размер пула подключений
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
            
            _connectionString = connectionString;
            _connectionPool = new ConcurrentQueue<SqlConnection>();

            // Создаем пул подключений и открываем их
            for (int i = 0; i < _poolSize; i++)
            {
                var connection = new SqlConnection(_connectionString);
                connection.Open();
                _connectionPool.Enqueue(connection);
            }
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
        /// Создает новый экземпляр DbContext с переиспользованием подключения из пула
        /// Подключение НЕ закрывается при Dispose() DbContext, а возвращается в пул
        /// </summary>
        public MyDbContext CreateDbContext()
        {
            // Получаем подключение из пула
            if (!_connectionPool.TryDequeue(out SqlConnection? connection))
            {
                // Если пул пуст, создаем новое подключение (но это не должно происходить часто)
                connection = new SqlConnection(_connectionString);
                connection.Open();
            }

            // Проверяем, что подключение открыто
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            // Создаем опции с переиспользованием подключения
            var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();
            optionsBuilder.UseSqlServer(connection, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            return new PooledDbContext(optionsBuilder.Options, connection);
        }

        /// <summary>
        /// Возвращает подключение в пул (вызывается из PooledDbContext при Dispose)
        /// </summary>
        internal void ReturnConnection(SqlConnection connection)
        {
            if (connection != null && connection.State == System.Data.ConnectionState.Open)
            {
                _connectionPool.Enqueue(connection);
            }
        }

        /// <summary>
        /// Освобождает ресурсы фабрики и закрывает все подключения
        /// </summary>
        public static void Dispose()
        {
            if (_instance != null)
            {
                lock (_instanceLock)
                {
                    if (_instance != null)
                    {
                        // Закрываем все подключения в пуле
                        while (_instance._connectionPool.TryDequeue(out SqlConnection? connection))
                        {
                            try
                            {
                                if (connection.State != System.Data.ConnectionState.Closed)
                                {
                                    connection.Close();
                                }
                                connection.Dispose();
                            }
                            catch { }
                        }
                        _instance = null;
                    }
                }
            }
        }
    }
}
