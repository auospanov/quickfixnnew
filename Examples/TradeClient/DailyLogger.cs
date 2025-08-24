using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeClient
{
using System;
using System.IO;

public static class DailyLogger
{
    private static readonly string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

    public static void Log(string message)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            string date = DateTime.Now.ToString("yyyy-MM-dd");
            string logFilePath = Path.Combine(logDirectory, $"log_{date}.txt");

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string fullMessage = $"[{timestamp}] {message}{Environment.NewLine}";

            File.AppendAllText(logFilePath, fullMessage);
        }
        catch (Exception ex)
        {
            // Если нужно, можно логировать в резерв или вывести на консоль
            Console.Error.WriteLine($"Ошибка логгирования: {ex.Message}");
        }
    }
}

}
