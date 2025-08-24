using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeClient
{
using RestSharp;
using System;
    using System.IO;
    using System.Threading.Tasks;

public class OrderSender
{
    //private const string ApiUrl = "https://api.stdi.kz/v3/kaseOrders/set";
   public static void writeLog(string request)
{
    try
    {
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string logDir = Path.Combine(basePath, "logs");
        string logPath = Path.Combine(logDir, $"{DateTime.Now:yyyy-MM-dd}.log");

        // Создаём директорию, если её нет
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        StringBuilder sb = new StringBuilder();
        string oldLog = "";

        try
        {
            if (File.Exists(logPath))
            {
                using (StreamReader sr = new StreamReader(logPath))
                {
                    oldLog = sr.ReadToEnd();
                }
            }
        }
        catch
        {
            // можно добавить логирование ошибок чтения, если нужно
        }

        try
        {
            sb.AppendLine("request: " + request);
        }
        catch
        {
            // можно добавить логирование ошибок форматирования
        }

        sb.AppendLine("# # # # #");
        sb.Append(oldLog);

        using (StreamWriter outfile = new StreamWriter(logPath, false))
        {
            outfile.Write(sb.ToString());
        }
    }
    catch
    {
        // можно добавить логирование ошибок верхнего уровня
    }
}

    public static void SendOrdersAsyncFireAndForget(string jsonOrders)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var client = new RestClient(Program.urlService);
                var request = new RestRequest();
                request.Method = Method.Post;
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", jsonOrders, ParameterType.RequestBody);
                if (!string.IsNullOrEmpty(Program.urlServiceAuthorization))
                    request.AddHeader("Authorization", Program.urlServiceAuthorization);
                
                var response = await client.ExecuteAsync(request);
                writeLog("Time: " + DateTime.Now + " jsonOrders = " + jsonOrders + $";\n Ответ API: {response.StatusCode} — {response.Content}");
                // Можно залогировать при необходимости
                Console.WriteLine($"[DEBUG] Ответ API: {response.StatusCode} — {response.Content}");
            }
            catch (Exception ex)
            {
                writeLog("Time: " + DateTime.Now + " jsonOrders = " + jsonOrders + "; Exception = " + ex.Message);
                Console.WriteLine($"[ERROR] Ошибка при отправке запроса: {ex.Message}");
            }
        });
    }
}

}
