using System;
using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace TradeClient
{
    /// <summary>
    /// Пул подключений к Oracle AIS (по аналогии с DbContextFactory для MSSQL).
    /// Использует встроенный пул Oracle.ManagedDataAccess (аналог Hikari по использованию).
    /// </summary>
    public static class OracleAisConnectionFactory
    {
        private static string _connectionString;
        private static readonly object _initLock = new object();
        private static bool _initialized;

        /// <summary>
        /// Инициализация фабрики строкой подключения к Oracle.
        /// Строка строится из параметров: DB_AIS_IP, DB_AIS_PORT, DB_AIS_NAME, DB_AIS_USER, DB_AIS_PASS.
        /// </summary>
        public static void Initialize(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                _initialized = false;
                return;
            }
            lock (_initLock)
            {
                _connectionString = connectionString;
                _initialized = true;
                Console.WriteLine("[OracleAisConnectionFactory] Пул подключений к Oracle AIS инициализирован");
            }
        }

        /// <summary>
        /// Создать подключение из пула (используйте using/Dispose для возврата в пул).
        /// </summary>
        public static OracleConnection GetConnection()
        {
            if (!_initialized || string.IsNullOrEmpty(_connectionString))
                throw new InvalidOperationException("OracleAisConnectionFactory не инициализирована. Вызовите Initialize() с параметрами DB_AIS_*.");
            return new OracleConnection(_connectionString);
        }

        /// <summary>
        /// Построить строку подключения Oracle из параметров конфига.
        /// </summary>
        public static string BuildConnectionString(string cfg)
        {
            string ip = Program.GetValueByKey(cfg, "DB_AIS_IP");
            string port = Program.GetValueByKey(cfg, "DB_AIS_PORT");
            string name = Program.GetValueByKey(cfg, "DB_AIS_NAME");
            string user = Program.GetValueByKey(cfg, "DB_AIS_USER");
            string pass = Program.GetValueByKey(cfg, "DB_AIS_PASS");
            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(user))
                return null;
            if (string.IsNullOrEmpty(port)) port = "1521";
            // Data Source в формате host:port/service_name (или SID для thin)
            string dataSource = $"{ip}:{port}/{name}";
            return $"Data Source={dataSource};User Id={user};Password={pass};Min Pool Size=1;Max Pool Size=10";
        }

        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Освобождение ресурсов (отключение пула).
        /// </summary>
        public static void DisposeStatic()
        {
            lock (_initLock)
            {
                _connectionString = null;
                _initialized = false;
                OracleConnection.ClearAllPools();
                Console.WriteLine("[OracleAisConnectionFactory] Пул Oracle AIS закрыт");
            }
        }
    }
}
