using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using QuickFix.Logger;
using QuickFix.Store;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using ClassLibrary2;

namespace TradeClient
{
    class Program
    {
        public static string cfg = "";
        public static string ADAPTER = ""; 
        public static byte ISREAL = 0;
        public static string EXCH_CODE = ""; //к какой бирже относится
        public static string urlService = "";
        public static string urlServiceAuthorization = "";
        [STAThread]
        static void Main(string[] args)
        {
            OrderSender.writeLog("test");
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Console.WriteLine($"[AppDomain] Unhandled exception: {ex?.Message}");
                DailyLogger.Log($"[AppDomain] Unhandled exception: {ex?.Message}");
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Console.WriteLine($"[TaskScheduler] Unobserved task exception: {args.Exception.Message}");
                args.SetObserved(); // чтобы не крашилось приложение
                DailyLogger.Log($"[TaskScheduler] Unobserved task exception: {args.Exception.Message}");
            };
            #if DEBUG
                ADAPTER = "kaseCurr"; // aix "Exante"; //тут указываем экземпляр обаботчика, например kaseDropCopy kaseCurr kaseCurrDropCopy kaseSpot kaseSpotDropCopy
#endif

            try {ADAPTER = args[0]; }catch(Exception ex){}
            //try{ISREAL = byte.Parse(args[1]); }catch(Exception ex){}
            /*string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" ?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><dlws:submitGetHistoryResponse xmlns='http://services.bloomberg.com/datalicense/dlws/ps/20071001' xmlns:dlws=\"http://services.bloomberg.com/datalicense/dlws/ps/20071001\" xmlns:env=\"http://schemas.xmlsoap.org/soap/envelope/\"><dlws:statusCode><dlws:code>0</dlws:code><dlws:description>Success</dlws:description></dlws:statusCode><dlws:requestId>4804a8c6-ec8e-43f9-96f1-02fa1de4a9a9</dlws:requestId><dlws:responseId>1745944921-1970589519</dlws:responseId></dlws:submitGetHistoryResponse></soap:Body></soap:Envelope>";
            var obj = BloombergSoapClient.ParseHistoryResponse(xml);
            string result = BloombergSoapClient.RetrieveHistoryResponseAsync(obj.ResponseId).Result;
            string resultXml = File.ReadAllText("C:\\inetpub\\wwwroot\\quickfixn\\Examples\\TradeClient\\resultXML.xml");
            var retrieveResponse = BloombergParser.ParseRetrieveHistoryResponse(resultXml);

            if (retrieveResponse != null)
            {
                Console.WriteLine($"RequestId: {retrieveResponse.RequestId}");
                Console.WriteLine($"ResponseId: {retrieveResponse.ResponseId}");
                Console.WriteLine($"Status: {retrieveResponse.StatusCode.Description}");

                foreach (var instrumentData in retrieveResponse.InstrumentDatas)
                {
                    Console.WriteLine($"Instrument: {instrumentData.Instrument.Id}");
                    Console.WriteLine($"Date: {instrumentData.Date}");

                    for (int i = 0; i < instrumentData.Data.Count; i++)
                    {
                        Console.WriteLine($"Data[{i}]: {instrumentData.Data[i].Value}");
                    }
                }
            }
            else
            {
                Console.WriteLine("Ошибка при разборе XML-ответа.");
            }
            string ress = KaseService.tratement().Result;
            string dealsxml = File.ReadAllText("C:\\inetpub\\wwwroot\\quickfixn\\Examples\\TradeClient\\deal.xml");
            List<KaseItem> items = KaseXmlManualParser.Parse(dealsxml);

            foreach (var i in items)
            {
                Console.WriteLine($"{i.date}: {i.ticker} — {i.priceWA} KZT");
            }
            */
            Console.WriteLine("=============");
            Console.WriteLine();
            Console.WriteLine("Start program");

            //if (args.Length != 1)
            //{
            //    System.Console.WriteLine("usage: TradeClient.exe CONFIG_FILENAME");
            //    System.Environment.Exit(2);
            //}

            //string file = "tradeclient.cfg";//  args[0];


            try
            {
//#if DEBUG
//                TradeClientApp.connection = System.Environment.GetEnvironmentVariable("HB_FixDb_TEST");
//                Console.WriteLine("TradeClientApp.connection " +TradeClientApp.connection);
//                //using (var context = new MyDbContext()) 
//                //{
//                //    cfg = context.settingsTP.Where(s => s.columnCode == "fix" + EXCHANGECODE + "Test").Select(s=>s.value1).FirstOrDefault().ToString(); fix + KASECurrDropCopy + Test
//                //}
//               cfg = File.ReadAllText("fixExante.cfg");

//#else
//                TradeClientApp.connection = System.Environment.GetEnvironmentVariable("HB_FixDb_PROD");
//                Console.WriteLine(TradeClientApp.connection);
//                //  using (var context = new MyDbContext()) 
//                //{
//                //    cfg = context.settingsTP.Where(s=>s.columnCode == "fix" + EXCHANGECODE + "Real").Select(s=>s.value1).FirstOrDefault().ToString(); //fix + KASECurrDropCopy + Real
//                //}
//#endif
                cfg = File.ReadAllText("fix_"+ ADAPTER + ".cfg");
                System.IO.TextReader textReader = new StringReader(cfg);
                QuickFix.SessionSettings settings = new QuickFix.SessionSettings(textReader);
                if (TradeClientApp.isStop(GetValueByKey(cfg,"ConnectionString"))) //TradeClientApp.connection))
                {
                    Environment.Exit(0);
                }
                urlService = GetValueByKey(cfg,"urlService");  
                urlServiceAuthorization = GetValueByKey(cfg,"urlServiceAuthorization"); 
                
                try{EXCH_CODE = GetValueByKey(cfg,"ExchangeCode"); } catch(Exception ex){};
                try{ISREAL = byte.Parse(GetValueByKey(cfg,"IsReal")); } catch(Exception ex){};

                //QuickFix.SessionSettings settings = new QuickFix.SessionSettings((file);

                //---------------------------------------------------------------------------------------
                // чтение и запись переменных из файла конфигурации для использования в DataDictionary
                string ValidateUserDefinedFields = "Y";
                try { ValidateUserDefinedFields = GetValueByKey(cfg, "ValidateUserDefinedFields"); }
                catch { }

                string ValidateFieldsHaveValues = "N";
                try { ValidateFieldsHaveValues = GetValueByKey(cfg, "ValidateFieldsHaveValues"); }
                catch { }

                string ValidateFieldsOutOfOrder = "N";
                try { ValidateFieldsOutOfOrder = GetValueByKey(cfg, "ValidateFieldsOutOfOrder"); }
                catch { }

                if (ValidateUserDefinedFields == "Y") SharedData.CheckUserDefinedFields = true;
                else SharedData.CheckUserDefinedFields = false;

                if (ValidateFieldsHaveValues == "Y") SharedData.CheckFieldsHaveValues = true;
                else SharedData.CheckFieldsHaveValues = false;

                if (ValidateFieldsOutOfOrder == "Y") SharedData.CheckFieldsOutOfOrder = true;
                else SharedData.CheckFieldsOutOfOrder = false;

               
                //---------------------------------------------------------------------------------------





                //SharedData.CheckFieldsHaveValues


                TradeClientApp application = new TradeClientApp(settings);
                IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
                //ILogFactory logFactory = new ScreenLogFactory(settings);
                ILogFactory logFactory = new FileLogFactory(settings);
                QuickFix.Transport.SocketInitiator initiator = new QuickFix.Transport.SocketInitiator(application, storeFactory, settings, logFactory);

                // this is a developer-test kludge.  do not emulate.
                application.MyInitiator = initiator;

                initiator.Start();
                application.Run();
                //initiator.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            Environment.Exit(1);
        }

        public static string GetValueByKey(string fileContent, string key)
{
    var lines = fileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    foreach (var line in lines)
    {
        var trimmed = line.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            continue;

        var parts = trimmed.Split(new[] { '=' }, 2);
        if (parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            return parts[1].Trim();
    }

    return null; // вернёт null, если ключ не найден
}
    }
}
