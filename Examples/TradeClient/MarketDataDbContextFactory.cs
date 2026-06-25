using System;
using System.Data;
using System.Data.SqlClient;

namespace TradeClient
{
    /// <summary>
    /// Отдельное подключение к БД маркет-данных (signalRMessages, fixDataUpdateNew для котировок).
    /// По аналогии с DbContextFactory — одно физическое соединение без пула.
    /// </summary>
    public static class MarketDataDbContextFactory
    {
        private static SqlConnection? _sharedConnection;
        private static readonly object _initLock = new object();

        public static bool IsInitialized { get; private set; }

        public static void Initialize(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                IsInitialized = false;
                return;
            }

            lock (_initLock)
            {
                if (_sharedConnection != null)
                    return;

                var cleanedConnectionString = SqlConnectionStringHelper.Clean(connectionString);
                _sharedConnection = new SqlConnection(cleanedConnectionString);
                _sharedConnection.Open();
                IsInitialized = true;
                Console.WriteLine($"[MarketDataDbContextFactory] Подключение к БД маркет-данных открыто. ConnectionId: {_sharedConnection.ClientConnectionId}");
            }
        }

        public static SqlConnection? GetSharedConnection() => _sharedConnection;

        public static void DisposeStatic()
        {
            lock (_initLock)
            {
                if (_sharedConnection == null)
                {
                    IsInitialized = false;
                    return;
                }

                try
                {
                    Console.WriteLine($"[MarketDataDbContextFactory] Закрытие подключения. ConnectionId: {_sharedConnection.ClientConnectionId}");
                    if (_sharedConnection.State != ConnectionState.Closed)
                        _sharedConnection.Close();
                    _sharedConnection.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MarketDataDbContextFactory] Ошибка при закрытии: {ex.Message}");
                }
                finally
                {
                    _sharedConnection = null;
                    IsInitialized = false;
                }
            }
        }
    }
}
