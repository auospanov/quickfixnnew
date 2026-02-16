using ClassLibrary2;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

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
        //public static string changePasswordDay = "";
        public static bool isChangePassword=false;
        public static string tmpPassword = string.Empty;
        public static string newPassword = string.Empty;
        public static bool isMustStartedAfterChangePassword = false;
        public static string checkNewOrdersIntervalMiliseconds = string.Empty;
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
                ADAPTER = "aix"; // aix "Exante"; //тут указываем экземпляр обаботчика, например kase kaseDropCopy kaseCurr kaseCurrDropCopy kaseSpot kaseSpotDropCopy its aix_SP1
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
                
                // Инициализация фабрики DbContext для управления пулом подключений
                string connectionString = GetValueByKey(cfg, "ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("Ошибка: ConnectionString не найден в конфигурации");
                    Environment.Exit(1);
                }
                
                // НЕ добавляем параметры пула подключений - пул отключен в DbContextFactory
                // для использования строго одного подключения
                
                DbContextFactory.Initialize(connectionString);
                Console.WriteLine("DbContextFactory инициализирована с пулом подключений");
                
                if (TradeClientApp.isStop(connectionString))
                {
                    DbContextFactory.DisposeStatic();
                    Environment.Exit(0);
                }
                // Пул подключений к Oracle AIS (для JYSAN/Tengri — заявки для отправки на FIX)
                string broker = GetValueByKey(cfg, "Broker") ?? GetValueByKey(cfg, "ExchangeCode") ?? "";
                if (broker.Equals("JYSAN", StringComparison.OrdinalIgnoreCase) || broker.Equals("Tengri", StringComparison.OrdinalIgnoreCase))
                {
                    string oracleAisCs = OracleAisConnectionFactory.BuildConnectionString(cfg);
                    if (!string.IsNullOrEmpty(oracleAisCs))
                    {
                        OracleAisConnectionFactory.Initialize(oracleAisCs);
                    }
                }
                checkNewOrdersIntervalMiliseconds = Program.GetValueByKey(Program.cfg, "checkNewOrdersIntervalMiliseconds");
                urlService = GetValueByKey(cfg,"urlService");  
                urlServiceAuthorization = GetValueByKey(cfg,"urlServiceAuthorization");
                string changePasswordDay = GetValueByKey(cfg, "changePasswordDay");
                if (changePasswordDay != null)
                {
                    bool ok = int.TryParse(changePasswordDay, out int numDay);
                    if (ok)
                    {
                        if ((int)DateTime.Today.DayOfWeek == numDay) isChangePassword = true;
                    }
                }

                try {EXCH_CODE = GetValueByKey(cfg,"ExchangeCode"); } catch(Exception ex){};
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

                /*
                 var app = new FailoverApp();
                 app.Start();
                 //app.Run();
                  */
                //while (true)
                //{
                //    if (TradeClientApp.isStop(Program.GetValueByKey(Program.cfg, "ConnectionString")))
                //    {
                //        initiator.Stop();
                //    }
                //}
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                OracleAisConnectionFactory.DisposeStatic();
                // Освобождаем ресурсы фабрики при завершении приложения
                DbContextFactory.DisposeStatic();
            }
            Environment.Exit(1);
        }
        static async Task ExitAfterDelayAsync(int milliseconds, QuickFix.Transport.SocketInitiator initiator)
        {
            await Task.Delay(milliseconds);
            initiator.Stop();
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
        public static bool checkNewPassword(string password)
        {
            try
            {
                string t1 = GetValueByKey(cfg, "oldPasswords");
                if(string.IsNullOrEmpty(t1))
                {
                    return false;
                }
                //List<string> oldPasswords = (List<string>)JsonConvert.DeserializeObject(t1);
                //var k1 = JsonConvert.DeserializeObject(t1);
                //var t2 = k1.GetType();
                var r1 = System.Text.Json.JsonSerializer.Deserialize<List<string>>(t1);
                if (r1.Contains(password)) return false;
                tmpPassword = password;
                return true;

            }
            catch { return false; }

        }
        public static void recNewPassword()
        {
            try
            {
                if (string.IsNullOrEmpty(tmpPassword)) return;

                string path = "fix_" + ADAPTER + ".cfg";
                string[] lines = File.ReadAllLines(path);

                // индекс строки с текущим паролем
                int pswIndex = Array.FindIndex(lines, r => r.StartsWith("Password="));
                if (pswIndex == -1) return;

                lines[pswIndex] = "Password=" + tmpPassword;

                // индекс строки со старыми паролями
                int oldIndex = Array.FindIndex(lines, r => r.StartsWith("oldPasswords="));
                if (oldIndex != -1)
                {
                    string oldPasswordsRaw = lines[oldIndex].Replace("oldPasswords=", "");
                    List<string>? lstPsw = System.Text.Json.JsonSerializer.Deserialize<List<string>>(oldPasswordsRaw);

                    if (lstPsw != null)
                    {
                        if (lstPsw.Count < 10)
                        {
                            lstPsw.Add(tmpPassword); // добавляем новый пароль
                        }
                        else
                        {
                            lstPsw.RemoveAt(0);
                            lstPsw.Add(tmpPassword);
                        }

                        string serialized = System.Text.Json.JsonSerializer.Serialize(lstPsw);
                        lines[oldIndex] = "oldPasswords=" + serialized;
                    }
                }

                File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при обновлении пароля: " + ex.Message);
            }
        }
    }
}
