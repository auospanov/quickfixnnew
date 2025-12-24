using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Collections.Specialized.BitVector32;
using ApplicationException = System.ApplicationException;
using Exception = System.Exception;
namespace TradeClient
{
    public class TradeClientApp : QuickFix.MessageCracker, QuickFix.IApplication
    {
        public bool isDebug = false;
        private SessionSettings _settings;
        public static string connection = "";// "Data Source=WIN-DUS0A072PNF\\SQLEXPRESS;Initial Catalog=drivers_beSQL_new;Persist Security Info=True;User ID=platformAdm;Password=Admin$12345";
        public static List<instrsView> instrs = new List<instrsView>();
        private Timer _timer;
        private readonly int _timerBatchCollectMilliseconds = 10_000; // например, 10 секунд
        private readonly int portionSendOrder = 100;
        //private readonly int _timerIntervalMilliseconds = int.Parse(Program.GetValueByKey(Program.cfg, "timerIntervalMilliseconds"));
        public TradeClientApp(SessionSettings settings)
        {

            _settings = settings;

#if DEBUG
            isDebug = true;
            //connection = System.Environment.GetEnvironmentVariable("HB_FixDb_TEST");
#else
            isDebug = false;
            //connection = System.Environment.GetEnvironmentVariable("HB_FixDb_PROD");
            
            }
#endif
            try
            {
                _timerBatchCollectMilliseconds = int.Parse(Program.GetValueByKey(Program.cfg, "timerBatchCollectMilliseconds"));
                portionSendOrder = int.Parse(Program.GetValueByKey(Program.cfg, "portionSendOrder"));
            }
            catch { }
        }
        private Session? _session = null;

        // This variable is a kludge for developer test purposes.  Don't do this on a production application.
        public IInitiator? MyInitiator = null;

        #region IApplication interface overrides

        public void OnCreate(SessionID sessionId)
        {
            _session = Session.LookupSession(sessionId);
            if (_session is null)
                throw new ApplicationException("Somehow session is not found");
        }
        public static bool isStop(String connectionString)
        {
            /*
             create PROCEDURE [dbo].[fixDataUpdateNew] 
--универсальная процедура, используемая для:
 -- 1) обработки данных (список инструментов, маркет-дата и стаканы), полученных через FIX-протокол и в выходном параметре выдается итоговое состояние инструмента или стакана
 -- 2) выдачи данных по запросу (например, котировки для клиента)
 @messageText   nvarchar(MAX),  --текст запроса в формате json
 @resCode    varchar(10) output, --код возврата
 @resultString  nvarchar(MAX) output, --результат в формате json
 @messageType   varchar(50) = 'Normal'
AS
BEGIN
 set @resCode = '0'
 set @resultString = 'OK'
 if @messageType = 'WORKING_STATUS'
  --если это запрос состояния запуска/остановки обработчиков FIX/Бридж и пр.
  begin  
   set @resCode = '0'
   set @resultString = 'OK'
  end
 return
END
GO
             */
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString.Replace("Trust Server Certificate=True", "")))
                {
                    conn.Open();

                    // 1.  create a command object identifying the stored procedure
                    SqlCommand cmd = new SqlCommand("dbo.fixDataUpdateNew", conn);

                    // 2. set the command object so it knows to execute a stored procedure
                    cmd.CommandType = CommandType.StoredProcedure;

                    // 3. add parameter to command, which will be passed to the stored procedure
                    var param = new SqlParameter("@messageText", SqlDbType.NVarChar, 1000);
                    param.Value = "{\"typeQuery\":\"SELECT\",\"objectType\":\"WORKING_STATUS\",\"exchangeCode\":\"" + Program.ADAPTER + "\",\"sourcename\":\"fix\",\"isreal\":\"" + Program.ISREAL + "\"}";
                    param.Direction = ParameterDirection.Input;

                    cmd.Parameters.Add(param);
                    param = new SqlParameter("@resCode", "-1");
                    param.Direction = ParameterDirection.InputOutput;
                    cmd.Parameters.Add(param);
                    param = new SqlParameter("@resultString", SqlDbType.NVarChar, 10);
                    param.Value = "START";
                    param.Direction = ParameterDirection.InputOutput;
                    cmd.Parameters.Add(param);
                    param = new SqlParameter("@messageType", "WORKING_STATUS");
                    param.Direction = ParameterDirection.Input;
                    cmd.Parameters.Add(param);
                    // execute the command
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        return cmd.Parameters["@resultString"].Value.ToString().ToUpper().Contains("STOP");
                    }
                }
            }
            catch (Exception err)
            {
            }
            return true;
        }


        private void TimerTick(object state)
        {
            if (Program.GetValueByKey(Program.cfg, "IsQuotesRequest") == "1") {
                using (var dataContext = new MyDbContext())
                {
                    List<quotesSimple> tempList = new List<quotesSimple>();
                    lock (quotesSimples)
                    {
                        tempList = quotesSimples.ToList();
                        quotesSimples = new ConcurrentBag<quotesSimple>();
                    }
                    tempList = tempList.OrderBy(s => int.Parse(s.msgNum)).ToList();
                    dataContext.quotesSimple.AddRange(tempList);
                    dataContext.SaveChanges();
                }
            }
            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1") {
                using (var dataContext = new MyDbContext())
                {
                    try {
                        List<orders> tempList = new List<orders>();
                        lock (OrdersCache)
                        {
                            tempList = OrdersCache.ToList();
                            OrdersCache = new ConcurrentBag<orders>();
                        }
                        tempList = tempList.OrderBy(s => s.msgNum).ToList();
                        if (tempList.Count() > 0)
                        {
                            dataContext.orders.AddRange(tempList);
                            dataContext.SaveChanges();
                            if (!string.IsNullOrEmpty(Program.urlService)) {
                                OrderSender.SendOrdersAsyncFireAndForget(JsonConvert.SerializeObject(tempList));
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        string mes = err.Message;
                    }

                }
            }

            if (isDebug)
                Console.WriteLine($"[{DateTime.Now}] Timer ticked!");
            if (isStop(Program.GetValueByKey(Program.cfg, "ConnectionString")))
            {
                Environment.Exit(0);
            }
            if (_session != null && _session.IsLoggedOn)
            {
                if (isDebug) Console.WriteLine("Session active. You can send periodic messages here.");

                if (Program.GetValueByKey(Program.cfg, "IsQuotesRequest") == "1")
                {
                    using (var db = new MyDbContext())
                    {
                        var intrsView = db.instrsView.ToList();

                        var updatedInstrs = new List<instrsView>();

                        foreach (var instrView in intrsView)
                        {
                            bool exists = instrs.Any(i => i.symbol == instrView.symbol);

                            if (!exists)
                            {
                                try
                                {
                                    string requestId = SendMarketDataRequest(_session.SessionID, instrView);

                                    updatedInstrs.Add(new instrsView
                                    {
                                        symbol = instrView.symbol,
                                        codeMubasher = instrView.codeMubasher,
                                        requestId = requestId
                                    });
                                }
                                catch (Exception ex)
                                {
                                    if (isDebug) Console.WriteLine($"Ошибка при подписке на инструмент {instrView.symbol}: {ex.Message}");
                                }
                            }
                            else
                            {
                                var existingInstr = instrs.First(i => i.symbol == instrView.symbol);
                                updatedInstrs.Add(existingInstr);
                            }
                        }

                        foreach (var oldInstr in instrs)
                        {
                            bool stillExists = intrsView.Any(i => i.symbol == oldInstr.symbol);

                            if (!stillExists)
                            {
                                try
                                {
                                    SendMarketDataUnsubscribe(_session.SessionID, oldInstr);
                                    if (isDebug) Console.WriteLine($"Отписка от инструмента: {oldInstr.symbol}");
                                }
                                catch (Exception ex)
                                {
                                    if (isDebug) Console.WriteLine($"Ошибка при отписке от инструмента {oldInstr.symbol}: {ex.Message}");
                                }
                            }
                        }

                        instrs = updatedInstrs;
                    }
                }
            }
            else
            {
                if (isDebug) Console.WriteLine("Session is not active. Skipping sending.");
            }
        }

        public void OnLogon(SessionID sessionId)
        {

            //запрос справочника инструментов
            if (Program.GetValueByKey(Program.cfg, "IsInstrRequest") == "1")
                try
                {
                    //
                }
                catch (Exception e)
                {
                }

            //запрос стутуса торгов
            if (Program.GetValueByKey(Program.cfg, "IsTradeStatusRequest") == "1")
                try
                {
                    //
                }
                catch (Exception e)
                {
                }

            //запрос котировок

            try
            {
                // Если таймер уже был создан — останавливаем его
                _timer?.Dispose();

                // Создаем и запускаем новый таймер
                _timer = new Timer(
                    callback: TimerTick, // метод который будет вызываться
                    state: null,
                    dueTime: 0,                // Запускать сразу
                    period: _timerBatchCollectMilliseconds // Периодичность
                );
                instrs = new List<instrsView>();
            }
            catch (Exception e)
            {
            }

            //SendMarketDataRequest(sessionId);
            if (isDebug) Console.WriteLine("Logon - " + sessionId);
        }
        public void OnLogout(SessionID sessionId)
        {
            if (isDebug) Console.WriteLine("Logout - " + sessionId);
            try
            {
                // Когда отключились — остановить таймер
                _timer?.Dispose();
                _timer = null;
                instrs = new List<instrsView>();
            }
            catch { }
        }

        public void FromAdmin(Message message, SessionID sessionId)
        {
            if (isDebug) Console.WriteLine("FromAdmin - " + message.ToString());



            if (message is QuickFix.FIXT11.Heartbeat || message is QuickFix.FIX44.Heartbeat)
            {
                try
                {
                    using (var db = new MyDbContext())
                    {
                        db.heartbeat.Add(new heartbeat() { exchangeCode = Program.ADAPTER, lastTime = DateTime.Now, isReal = Program.ISREAL, isMM = 0 });
                        db.SaveChanges();
                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[Heartbeat] OnMessage : {e.Message} " + JsonConvert.SerializeObject(message));
                }
            }
            else if (message is QuickFix.FIXT11.Reject m)
            {
                if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
                {
                    if (isDebug) Console.WriteLine("Received BusinessMessageReject");
                    try
                    {
                        using (var db = new MyDbContext())
                        {
                            int clOrdId = 0;

                            try { clOrdId = db.NewOrders.Where(r => r.msgNum == int.Parse(m.RefSeqNum.Value.ToString())).OrderByDescending(r => r.Id).Select(r => r.Id).FirstOrDefault(); } catch { }

                            var order = new orders();
                            order.msgNum = int.Parse(m.Header.GetString(34));
                            order.orderReferenceExchange = $"MsgType = {m.Header.GetString(35)}";
                            order.status = "REJECTED";
                            order.clientID = "RefMsgType = " + m.RefMsgType.Value;
                            order.executionTime = m.Header.GetDateTime(QuickFix.Fields.Tags.SendingTime);
                            order.fullMessage = m.ToJSON();
                            string text = m.IsSetField(QuickFix.Fields.Tags.Text) ? m.GetString(QuickFix.Fields.Tags.Text) : string.Empty;
                            order.comments = $"Tag={m.RefTagID}, Reason={m.SessionRejectReason} {text}";
                            order.exchangeCode = Program.EXCH_CODE;
                            order.serial = m.RefSeqNum.Value.ToString();

                            if (clOrdId > 0) order.clientOrderID = clOrdId.ToString();

                            db.orders.Add(order);
                            db.SaveChanges();
                        }
                    }
                    catch (Exception e)
                    {
                        DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                    }
                }

            }
            else if (message is QuickFix.FIX44.Logon logon)
            {
                //если сегодня меняется пароль
                if (Program.isChangePassword)
                {
                    string rz58 = string.Empty;
                    try { rz58 = logon.GetString(1409); }
                    catch { }
                    //если найден тег 58, то проверка на код "209" - пароль изменен успешно
                    if (rz58 == "0")
                    {
                        //запись в конфигурационный файл нового пароля
                        Program.recNewPassword();
                    }

                }

            }

        }
        public void ToAdmin(Message message, SessionID sessionId)
        {
            if (message.Header.GetString(QuickFix.Fields.Tags.MsgType) == QuickFix.Fields.MsgType.LOGON)
            {
                var sessionConfig = _settings.Get(sessionId);

                if (sessionConfig.Has("Username") && sessionConfig.Has("Password")) //add 03.11.2025 for AIX
                {
                    message.SetField(new QuickFix.Fields.Username(sessionConfig.GetString("Username")));
                    message.SetField(new QuickFix.Fields.Password(sessionConfig.GetString("Password")));
                    if (isDebug) Console.WriteLine($"Injected Username/Password for session {sessionId}");
                }
                else if (sessionConfig.Has("Password")) //(sessionConfig.Has("SenderCompID") && 
                {
                    //message.SetField(new QuickFix.Fields.Username(sessionConfig.GetString("SenderCompID")));
                    message.SetField(new QuickFix.Fields.Password(sessionConfig.GetString("Password")));
                    if (isDebug) Console.WriteLine($"Injected Username/Password for session {sessionId}");
                }
                else
                {
                    if (isDebug) Console.WriteLine("Username or Password not found in settings");
                }
                if (Program.isChangePassword)
                {
                    string newPassword = GenerateNewPassword();
                    bool ok = false;
                    while (!ok)
                    {
                        newPassword = GenerateNewPassword();
                        ok = Program.checkNewPassword(newPassword);
                    }
                    //newPassword = "4444qwer";

                    message.SetField(new NewPassword(newPassword));
                }
                //временно добавил 07.11.2025
                if (sessionConfig.Has("ResetSeqNumFlag"))
                {
                    bool val = sessionConfig.GetString("ResetSeqNumFlag") == "Y" ? true : false;
                    message.SetField(new ResetSeqNumFlag(val));
                    // logon.Set(new ResetSeqNumFlag(true)); // Сбросить счётчик сообщений

                }
                if (sessionConfig.Has("HeartBtInt"))
                {
                    int val = 0;
                    try { val = int.Parse(sessionConfig.GetString("HeartBtInt")); } catch { }
                    if (val > 0)
                        message.SetField(new HeartBtInt(val));
                }
            }
        }
        public void FromApp(Message message, SessionID sessionId)
        {
            if (isDebug) Console.WriteLine("IN:  " + message.ConstructString());
            try
            {
                Crack(message, sessionId);
                Console.WriteLine($"[FromApp] {message}");
            }
            catch (Exception ex)
            {
                if (isDebug) Console.WriteLine("==Cracker exception==");
                if (isDebug) Console.WriteLine(ex.ToString());
                if (isDebug) Console.WriteLine(ex.StackTrace);
            }
        }

        public void ToApp(Message message, SessionID sessionId)
        {
            try
            {
                bool possDupFlag = false;
                if (message.Header.IsSetField(Tags.PossDupFlag))
                {
                    possDupFlag = message.Header.GetBoolean(Tags.PossDupFlag);
                }
                if (possDupFlag)
                    throw new DoNotSend();
            }
            catch (FieldNotFoundException)
            { }

            int seqNum = 0;
            int clOrdId = 0;

            if (message is QuickFix.FIX44.NewOrderSingle || message is QuickFix.FIX50SP2.NewOrderSingle || message is QuickFix.FIX50SP1.NewOrderSingle)
            {
                seqNum = message.Header.GetInt(Tags.MsgSeqNum);
                clOrdId = int.Parse(message.GetString(11));
                try
                {
                    using (var db = new MyDbContext())
                    {
                        var oneRec = db.NewOrders.Where(r=>r.Id==clOrdId).FirstOrDefault();
                        if(oneRec != null)
                        {
                            oneRec.msgNum = seqNum;
                            db.SaveChanges();
                        }                        
                    }
                }
                catch (Exception e)
                {
                    OrderSender.writeLog("Exception oneRec = " + e.Message);
                }

            }

            if (isDebug) Console.WriteLine();
            if(isDebug) Console.WriteLine("OUT: " + message.ConstructString());
        }
        #endregion
        public class FixOrder
{
    public string ClOrdID { get; set; }
    public string OrigClOrdID { get; set; }
    public string Serial { get; set; }
    public string OrderReferenceExchange { get; set; }
    public string Comment { get; set; }
    public string Instr { get; set; }
    public string Acc { get; set; }
    public string Investor { get; set; }
    public string Currency { get; set; }
    public string Bloom_ExchCode { get; set; }
    public string SessionId { get; set; }
    public string ContrBroker { get; set; }

    public char? Side { get; set; }

    public decimal? LeavesQty { get; set; }
    public decimal? Price { get; set; }
    public decimal? Qty { get; set; }
    public decimal? PriceDeal { get; set; }
    public long? QtyDeal { get; set; }
    public decimal? PriceAvg { get; set; }
    public long? QtyTotal { get; set; }

    public string ExpireDate { get; set; }
    public string TimeInForce { get; set; }
    public decimal? CashQty { get; set; }
    public string Status { get; set; }

    public DateTime? CreationTime { get; set; }
    public string ExecutionTimeStr { get; set; }
    public string SettlementDateStr { get; set; }

    public string WhoRemoved { get; set; }
    public DateTime? WhenRemoved { get; set; }

    public string IsMMOrder { get; set; }

    public string UnderlyingInstr { get; set; }
    public long? UnderlyingInstrQty { get; set; }

    public DateTime? CloseDate { get; set; }

    public string Type { get; set; }
    public decimal? Yield { get; set; }
    public decimal? RepoTax { get; set; }
    public string RiskLevel { get; set; }
    public double? ClosePrice { get; set; }
    public string TrdMatchID { get; set; }

    public string UserName { get; set; }

    public string TypeQuery { get; set; }
    public string ObjectType { get; set; }
    public string ExchCode { get; set; }
    public byte? IsReal { get; set; }
}


        #region MessageCracker handlers
    //public void OnMessage(QuickFix.FIX44.ExecutionReport m, SessionID s)
    //{
    //    if (isDebug) Console.WriteLine("Received ExecutionReport");

    //    using (var db = new MyDbContext())
    //    {
    //        var order = new FixOrder(); // аналог Java Order

    //        // ClOrdID и OrigClOrdID
    //        if (m.ExecType.getValue() == QuickFix.Fields.ExecType.CANCELED || m.ExecType.getValue() == QuickFix.Fields.ExecType.PENDING_CANCEL)
    //        {
    //            if (m.IsSetField(11)) order.ClOrdID = m.GetString(11);
    //            if (m.IsSetField(41)) order.OrigClOrdID = m.GetString(41);
    //        }
    //        else if (m.ExecType.getValue() == QuickFix.Fields.ExecType.REJECTED)
    //        {
    //            if (m.IsSetField(11)) order.ClOrdID = m.GetString(11);
    //        }
    //        else
    //        {
    //            if (m.IsSetField(11)) order.ClOrdID = m.GetString(11); // аналог paramExecutionReport.getRef()
    //        }

    //        // ExecID (Serial)
    //        if (m.IsSetField(17)) order.Serial = m.GetString(17);

    //        // OrderID (Reference on exchange)
    //        if (m.IsSetField(37)) order.OrderReferenceExchange = m.GetString(37);

    //        // Comment / Text
    //        if (m.IsSetField(58))
    //        {
    //            string comment = m.GetString(58);
    //            if (FIXClient.FIX_EXCHANGECODE.StartsWith("KASE", StringComparison.OrdinalIgnoreCase))
    //            {
    //                order.Comment = Encoding.GetEncoding("windows-1251").GetString(Encoding.GetEncoding("windows-1252").GetBytes(comment));
    //            }
    //            else
    //            {
    //                order.Comment = comment;
    //            }
    //        }

    //        // Instr Symbol
    //        if (m.IsSetField(55)) order.Instr = m.GetString(55);

    //        // Account
    //        if (m.IsSetField(1)) order.Acc = m.GetString(1);

    //        // Investor PartyId (tag 448 in group 453 usually)
    //        if (m.IsSetField(448)) order.Investor = m.GetString(448);

    //        // Currency
    //        if (FIXClient.FIX_EXCHANGECODE.StartsWith("KASE", StringComparison.OrdinalIgnoreCase))
    //        {
    //            if (m.IsSetField(6029)) order.Currency = m.GetString(6029);
    //        }
    //        else if (FIXClient.FIX_EXCHANGECODE.Equals("QUIK", StringComparison.OrdinalIgnoreCase))
    //        {
    //            if (m.IsSetField(15)) order.Currency = m.GetString(15);
    //            if (m.IsSetField(207)) order.Bloom_ExchCode = m.GetString(207);
    //            if (m.IsSetField(100)) order.SessionId = m.GetString(100);
    //        }
    //        else if (FIXClient.FIX_EXCHANGECODE.Equals("Bloomberg", StringComparison.OrdinalIgnoreCase))
    //        {
    //            if (m.IsSetField(15)) order.Currency = m.GetString(15);
    //            if (m.IsSetField(207)) order.Bloom_ExchCode = m.GetString(207);
    //            if (m.IsSetField(76)) order.ContrBroker = m.GetString(76);
    //        }

    //        // Side
    //        if (m.IsSetField(54)) order.Side = m.GetChar(54);

    //        // LeavesQty
    //        if (m.IsSetField(151)) order.LeavesQty = m.GetDecimal(151);

    //        // Price
    //        if (m.IsSetField(44)) order.Price = m.GetDecimal(44);

    //        // OrderQty
    //        if (m.IsSetField(38)) order.Qty = m.GetDecimal(38);

    //        // LastPrice
    //        if (m.IsSetField(31)) order.PriceDeal = m.GetDecimal(31);

    //        // LastQty
    //        if (m.IsSetField(32)) order.QtyDeal = (long)m.GetDecimal(32);

    //        // AvgPx
    //        if (m.IsSetField(6)) order.PriceAvg = m.GetDecimal(6);

    //        // CumQty
    //        if (m.IsSetField(14)) order.QtyTotal = (long)m.GetDecimal(14);

    //        // ExpireDate
    //        if (m.IsSetField(126)) order.ExpireDate = m.GetString(126);

    //        // TimeInForce
    //        if (m.IsSetField(59)) order.TimeInForce = m.GetChar(59).ToString();

    //        // OrderCashQty
    //        if (m.IsSetField(921)) order.CashQty = m.GetDecimal(921);

    //        // OrderStatus
    //        if (m.IsSetField(39)) order.Status = m.GetChar(39).ToString();

    //        // TransactionTime
    //        if (m.IsSetField(60))
    //        {
    //            var transTime = m.GetUtcTimeStamp(60);
    //            if (FIXClient.FIX_EXCHANGECODE.StartsWith("KASE") || FIXClient.FIX_EXCHANGECODE.Equals("AIX", StringComparison.OrdinalIgnoreCase))
    //            {
    //                order.CreationTime = transTime.ToLocalTime();
    //            }
    //            else
    //            {
    //                order.CreationTime = transTime;
    //            }
    //            order.ExecutionTimeStr = transTime.ToString("yyyy-MM-dd HH:mm:ss");
    //        }

    //        // SettlementDate
    //        if (m.IsSetField(64))
    //        {
    //            order.SettlementDateStr = m.GetString(64);
    //        }

    //        // WhoRemoved / RemovedTime
    //        if (m.IsSetField(10500)) order.WhoRemoved = m.GetString(10500); // кастомный тег
    //        if (m.IsSetField(10501)) order.WhenRemoved = m.GetUtcTimeStamp(10501); // кастомный тег

    //        // OrderRestrictions (MM Order flag)
    //        if (m.IsSetField(529))
    //        {
    //            order.IsMMOrder = m.GetString(529) == "5" ? "1" : "0";
    //        }
    //        else
    //        {
    //            order.IsMMOrder = "0";
    //        }

    //        // Group 711: Underlying instruments
    //        if (m.NoUnderlyings.getValue() > 0)
    //        {
    //            for (int i = 1; i <= m.NoUnderlyings.getValue(); i++)
    //            {
    //                var group = new QuickFix.FIX44.ExecutionReport.NoUnderlyingsGroup();
    //                m.GetGroup(i, group);

    //                if (group.IsSetField(311)) order.UnderlyingInstr = group.GetString(311);
    //                if (group.IsSetField(879)) order.UnderlyingInstrQty = group.GetLong(879);
    //            }
    //        }

    //        // Yield
    //        if (m.IsSetField(236)) order.Yield = m.GetDecimal(236);

    //        // OrderType
    //        if (m.IsSetField(40)) order.Type = m.GetChar(40).ToString();

    //        // TrdMatchID (880)
    //        if (m.IsSetField(880)) order.TrdMatchID = m.GetString(880);

    //        // UserName from raw FIX string (tag 57)
    //        try
    //        {
    //            string raw = m.ToString().Replace('\u0001', ' ');
    //            int beg = raw.IndexOf(" 57=") + 4;
    //            int end = raw.IndexOf(" ", beg);
    //            if (beg > 3 && end > beg)
    //            {
    //                order.UserName = raw.Substring(beg, end - beg).Trim();
    //            }
    //        }
    //        catch { }

    //        // Прочие поля
    //        order.TypeQuery = "update";
    //        order.ObjectType = "fix_order";
    //        order.ExchCode = FIXClient.FIX_EXCHANGECODE;
    //        order.IsReal = FIXClient.FIX_ISREAL;

    //        // Добавить в БД или временный список
    //        // db.FixOrders.Add(order);
    //        // db.SaveChanges();
    //        FixOrdersList.Add(order); // если временная коллекция
    //    }
    //}
        public void OnMessage(QuickFix.FIX44.Heartbeat m, SessionID s)
        {
            try { 
             using (var db = new MyDbContext())
             {
                db.heartbeat.Add(new heartbeat() {  exchangeCode = Program.ADAPTER, lastTime  =DateTime.Now, isReal = Program.ISREAL, isMM = 0});
                db.SaveChanges();
             }
            }
            catch(Exception e) {
                DailyLogger.Log($"[Heartbeat] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
            }
        }
  
        public void OnMessage(QuickFix.FIX44.ExecutionReport m, SessionID s)
        {
            if (Program.GetValueByKey(Program.cfg,"IsWriteOrder") == "1")
            { 
                if (isDebug) Console.WriteLine("Received ExecutionReport");
                try { 
                     using (var db = new MyDbContext())
                {
                    var order = new orders();

                    // ClOrdID и OrigClOrdID
                    if (m.ExecType.getValue() == QuickFix.Fields.ExecType.CANCELED || m.ExecType.getValue() == QuickFix.Fields.ExecType.PENDING_CANCEL)
                    {
                        if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                        if (m.IsSetField(41)) order.origClOrderID = m.GetField(new QuickFix.Fields.OrigClOrdID()).getValue();
                    }
                    else if (m.ExecType.getValue() == QuickFix.Fields.ExecType.REJECTED)
                    {
                        if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                    }
                    else
                    {
                        if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                    }

                    if (m.IsSetField(17)) order.serial = m.GetField(new QuickFix.Fields.ExecID()).getValue();
                    if (m.IsSetField(37)) order.orderReferenceExchange = m.GetField(new QuickFix.Fields.OrderID()).getValue();

                    if (m.IsSetField(58))
                    {
                        string comment = m.GetField(new QuickFix.Fields.Text()).getValue();
                        if (Program.EXCH_CODE.Contains("KASE", StringComparison.OrdinalIgnoreCase))
                        {
                            order.comments = Encoding.GetEncoding("windows-1251").GetString(Encoding.GetEncoding("windows-1252").GetBytes(comment));
                        }
                        else
                        {
                            order.comments = comment;
                        }
                    }

                    if (m.IsSetField(55)) order.ticker = m.GetField(new QuickFix.Fields.Symbol()).getValue();
                    if (Program.EXCH_CODE.Contains("KASE", StringComparison.OrdinalIgnoreCase) && m.IsSetField(336))
                    {
                        order.board = m.GetString(336);
                    }
                    if (m.IsSetField(1)) order.acc = m.GetField(new QuickFix.Fields.Account()).getValue();
                    if (m.IsSetField(448)) order.investor = m.GetField(new QuickFix.Fields.PartyID()).getValue();

                    if (Program.EXCH_CODE.Contains("KASE", StringComparison.OrdinalIgnoreCase))
                    {
                        if (m.IsSetField(6029)) order.currency = m.GetString(6029);
                    }
                    else if (Program.EXCH_CODE.Equals("QUIK", StringComparison.OrdinalIgnoreCase))
                    {
                        if (m.IsSetField(15)) order.currency = m.GetField(new QuickFix.Fields.Currency()).getValue();
                        if (m.IsSetField(207)) order.bloom_exchCode = m.GetField(new QuickFix.Fields.SecurityExchange()).getValue();
                        if (m.IsSetField(100)) order.sessionId = m.GetField(new QuickFix.Fields.ExDestination()).getValue();
                    }
                    else if (Program.EXCH_CODE.Equals("Bloomberg", StringComparison.OrdinalIgnoreCase))
                    {
                        if (m.IsSetField(15)) order.currency = m.GetField(new QuickFix.Fields.Currency()).getValue();
                        if (m.IsSetField(207)) order.bloom_exchCode = m.GetField(new QuickFix.Fields.SecurityExchange()).getValue();
                        //if (m.IsSetField(76)) order.contrBroker = m.GetField(new QuickFix.Fields.ExecBroker()).getValue();
                    }

                    if (m.IsSetField(54)) 
                        //order.direction = m.GetField(new QuickFix.Fields.Side()).getValue().ToString(); d
                        order.direction = GetSideName(m.GetChar(54));
                    if (m.IsSetField(151)) order.leavesQty = m.GetDecimal(151);
                    if (m.IsSetField(44)) order.price = m.GetDecimal(44);
                    if (m.IsSetField(38)) order.quantity = m.GetDecimal(38);
                    if (m.IsSetField(31)) order.priceDeal = m.GetDecimal(31);
                    if (m.IsSetField(32)) order.quantityDeal = (long)m.GetDecimal(32);
                    if (m.IsSetField(6)) order.priceAvg = m.GetDecimal(6);
                    if (m.IsSetField(14)) order.quantityDealTotal = (long)m.GetDecimal(14);
                    //if (m.IsSetField(126)) order.expirationDate = DateTime.Parse(m.GetString(126));
                    try
                    {
                        order.expirationDate = DateTime.ParseExact(m.ExpireDate.Value, "yyyyMMdd", CultureInfo.InvariantCulture);
                    }
                    catch { }
                    if (m.IsSetField(59)) 
                    //order.timeInForce = m.GetChar(59).ToString(); d
                        order.timeInForce = GetTimeInForceName(m.GetChar(59));

                    //if (m.IsSetField(921)) order.cashQty = m.GetDecimal(921);
                    if (m.IsSetField(39)) 
                        //order.status = m.GetChar(39).ToString(); d
                        order.status = GetOrdStatusName(m.GetChar(39));
                    if (m.IsSetField(60))
                    {
                        var field = new QuickFix.Fields.TransactTime();
                        m.GetField(field);
                        DateTime transTime = field.getValue();
                        if (Program.EXCH_CODE.Equals("KASE") || Program.EXCH_CODE.Equals("AIX", StringComparison.OrdinalIgnoreCase))
                        {
                            order.executionTime = transTime.ToLocalTime();
                        }
                        else
                        {
                            order.executionTime = transTime;
                        }
                        //order.ExecutionTimeStr = transTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }

                    if (m.IsSetField(64)) order.settlementDate = m.GetDateOnly(64);
                    if (m.IsSetField(10500)) order.whoRemoved = m.GetString(10500);
                    if (m.IsSetField(10501))
                    {
                        try
                        {
                            var rawTimestamp = m.GetString(10501);
                            if (DateTime.TryParseExact(rawTimestamp, "yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                            {
                                order.whenRemoved = parsed;
                            }
                            else
                            {
                                order.whenRemoved = DateTime.Parse(rawTimestamp); // fallback
                            }
                        }
                        catch
                        {
                            order.whenRemoved = null;// DateTime.MinValue; // default fallback
                        }
                    }

                    if (m.IsSetField(529))
                    {
                        order.isMMorder = m.GetString(529) == "5" ? (byte)1 :  (byte)0;
                    }
                    else
                    {
                        order.isMMorder =  (byte)0;
                    }
                    try { 
                    if (m.NoUnderlyings.getValue() > 0)
                    {
                        for (int i = 1; i <= m.NoUnderlyings.getValue(); i++)
                        {
                            var group = new QuickFix.FIX44.ExecutionReport.NoUnderlyingsGroup();
                            m.GetGroup(i, group);

                            if (group.IsSetField(311)) order.underlyingInstr = group.GetString(311);
                            if (group.IsSetField(879))
                            {
                                var qty = new QuickFix.Fields.UnderlyingQty();
                                group.GetField(qty);
                                order.underlyingInstrQty = qty.getValue().ToString();
                            }
                        }
                    }
                    }catch(Exception err) { }
                    if (m.IsSetField(236)) order.yield = m.GetDecimal(236);
                    if (m.IsSetField(40)) order.type = GetOrdTypeName(m.GetChar(40));
                    if (m.IsSetField(880)) order.TrdMatchID = m.GetString(880);

                    try
                    {
                        string raw = m.ToString().Replace('\u0001', ' ');
                        int beg = raw.IndexOf(" 57=") + 4;
                        int end = raw.IndexOf(" ", beg);
                        if (beg > 3 && end > beg)
                        {
                            order.UserName = raw.Substring(beg, end - beg).Trim();
                        }
                    }
                    catch { }

            
            
                    order.exchangeCode = Program.EXCH_CODE;
                    order.isReal = Program.ISREAL;
                    order.msgNum = int.Parse(m.Header.GetString(34));
                    OrdersCache.Add(order); // замените на нужную вам структуру хранения (например, Add to List/Queue/Db)
                }
                }
                catch(Exception e) {
                    DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }
        }
        public void OnMessage(QuickFix.FIX50SP1.ExecutionReport m, SessionID s)
        {
            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
            {
                if (isDebug) Console.WriteLine("Received ExecutionReport");
                try
                {
                    if (Program.EXCH_CODE == "AIX_SP1")
                    {
                        using (var db = new MyDbContext())
                        {
                            var order = new orders();

                            // ClOrdID и OrigClOrdID
                            if (m.ExecType.getValue() == QuickFix.Fields.ExecType.CANCELED || m.ExecType.getValue() == QuickFix.Fields.ExecType.PENDING_CANCEL)
                            {
                                if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                                if (m.IsSetField(41)) order.origClOrderID = m.GetField(new QuickFix.Fields.OrigClOrdID()).getValue();
                            }
                            else if (m.ExecType.getValue() == QuickFix.Fields.ExecType.REJECTED)
                            {
                                if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                            }
                            else
                            {
                                if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                            }

                            if (m.IsSetField(17)) order.serial = m.GetField(new QuickFix.Fields.ExecID()).getValue();
                            if (m.IsSetField(37)) order.orderReferenceExchange = m.GetField(new QuickFix.Fields.OrderID()).getValue();

                            if (m.IsSetField(58))
                            {
                                order.comments = m.GetField(new QuickFix.Fields.Text()).getValue();
                            }

                            if (m.IsSetField(55)) order.ticker = m.GetField(new QuickFix.Fields.Symbol()).getValue();
                            if (m.IsSetField(1)) order.acc = m.GetField(new QuickFix.Fields.Account()).getValue();
                            if (m.IsSetField(448)) order.investor = m.GetField(new QuickFix.Fields.PartyID()).getValue();
                                                        

                            if (m.IsSetField(54)) order.direction = GetSideName(m.GetChar(54));
                            if (m.IsSetField(151)) order.leavesQty = m.GetDecimal(151);
                            if (m.IsSetField(44)) order.price = m.GetDecimal(44);
                            if (m.IsSetField(38)) order.quantity = m.GetDecimal(38);
                            if (m.IsSetField(31)) order.priceDeal = m.GetDecimal(31);
                            if (m.IsSetField(32)) order.quantityDeal = (long)m.GetDecimal(32);
                            if (m.IsSetField(6)) order.priceAvg = m.GetDecimal(6);
                            if (m.IsSetField(14)) order.quantityDealTotal = (long)m.GetDecimal(14);
                            if (m.IsSetField(59)) order.timeInForce = GetTimeInForceName(m.GetChar(59));
                            try
                            {
                                order.expirationDate = DateTime.ParseExact(m.ExpireDate.Value, "yyyyMMdd", CultureInfo.InvariantCulture);
                            }
                            catch { }

                            if (m.IsSetField(39)) order.status = GetOrdStatusName(m.GetChar(39));
                            if (m.IsSetField(60))
                            {
                                var field = new QuickFix.Fields.TransactTime();
                                m.GetField(field);
                                DateTime transTime = field.getValue();
                                if (Program.EXCH_CODE.Equals("KASE") || Program.EXCH_CODE.Contains("AIX", StringComparison.OrdinalIgnoreCase))
                                {
                                    order.executionTime = transTime.ToLocalTime();
                                }
                                else
                                {
                                    order.executionTime = transTime;
                                }
                                
                            }

                            if (m.IsSetField(64)) order.settlementDate = m.GetDateOnly(64);
                            if (m.IsSetField(529))
                            {
                                order.isMMorder = m.GetString(529) == "5" ? (byte)1 : (byte)0;
                            }
                            else
                            {
                                order.isMMorder = (byte)0;
                            }
                            try
                            {
                                if (m.NoUnderlyings.getValue() > 0)
                                {
                                    for (int i = 1; i <= m.NoUnderlyings.getValue(); i++)
                                    {
                                        var group = new QuickFix.FIX44.ExecutionReport.NoUnderlyingsGroup();
                                        m.GetGroup(i, group);

                                        if (group.IsSetField(311)) order.underlyingInstr = group.GetString(311);
                                        if (group.IsSetField(879))
                                        {
                                            var qty = new QuickFix.Fields.UnderlyingQty();
                                            group.GetField(qty);
                                            order.underlyingInstrQty = qty.getValue().ToString();
                                        }
                                    }
                                }
                            }
                            catch (Exception err) { }
                            if (m.IsSetField(236)) order.yield = m.GetDecimal(236);
                            if (m.IsSetField(40)) order.type = GetOrdTypeName(m.GetChar(40));
                            if (m.IsSetField(880)) order.TrdMatchID = m.GetString(880);

                            try
                            {
                                string raw = m.ToString().Replace('\u0001', ' ');
                                int beg = raw.IndexOf(" 57=") + 4;
                                int end = raw.IndexOf(" ", beg);
                                if (beg > 3 && end > beg)
                                {
                                    order.UserName = raw.Substring(beg, end - beg).Trim();
                                }
                            }
                            catch { }
                            if (m.IsSetField(111)) order.maxFloor = m.GetDecimal(111);

                            order.exchangeCode = Program.EXCH_CODE;
                            order.isReal = Program.ISREAL;
                            order.msgNum = int.Parse(m.Header.GetString(34));
                            order.fullMessage = m.ToJSON();
                            OrdersCache.Add(order); // замените на нужную вам структуру хранения (например, Add to List/Queue/Db)
                        }
                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }
        }
       
        public void OnMessage(QuickFix.FIX50SP2.ExecutionReport m, SessionID s)
        {
            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
            {
                if (isDebug) Console.WriteLine("Received ExecutionReport");
                try
                {
                    if (Program.EXCH_CODE == "ITS")
                    {
                        using (var db = new MyDbContext())
                        {
                            var ord = new orders();
                            ord.msgNum = long.Parse(m.Header.GetString(34));
                            ord.fullMessage = m.ToJSON();
                            ord.exchangeCode = Program.EXCH_CODE;
                            if (m.IsSetField(1)) ord.acc = m.Account.Value;
                            if (m.IsSetField(100)) ord.exDestination = m.GetField(new QuickFix.Fields.ExDestination()).Value;
                            if (m.IsSetField(10104)) ord.price1 = m.GetDecimal(10104);
                            if (m.IsSetField(103)) ord.ordRejReason = m.GetInt(103);
                            if (m.IsSetField(1080)) ord.refOrderId = m.GetString(1080);
                            if (m.IsSetField(11)) ord.clientOrderID = m.ClOrdID.Value;
                            if (m.IsSetField(41)) ord.origClOrderID = m.OrigClOrdID.Value;
                            if (m.IsSetField(150)) ord.execType = m.ExecType.Value.ToString();
                            if (m.IsSetField(151)) ord.leavesQty = m.LeavesQty.Value;
                            if (m.IsSetField(198)) ord.secondaryOrderId = m.GetString(198);
                            if (m.IsSetField(30)) ord.lastMkt = m.GetString(30);                                                        
                            if (m.IsSetField(37)) ord.orderReferenceExchange = m.GetString(37);
                            if (m.IsSetField(38)) ord.quantity = m.OrderQty.Value;
                            if (m.IsSetField(39)) ord.status = GetOrdStatusName(m.OrdStatus.Value);
                            if (m.IsSetField(40)) ord.type = GetOrdTypeName(m.OrdType.Value);
                            if (m.IsSetField(44)) ord.price = m.Price.Value;
                            if (m.IsSetField(48)) ord.ticker = m.SecurityID.Value;
                            if (m.IsSetField(54)) ord.direction = GetSideName(m.Side.Value);
                            if (m.IsSetField(59)) ord.timeInForce = GetTimeInForceName(m.TimeInForce.Value);
                            if (m.IsSetField(58)) ord.comments = m.Text.Value;
                            if (m.IsSetField(378)) ord.execRestatementReason = m.GetInt(378);
                            if (m.IsSetField(880)) ord.TrdMatchID = m.TrdMatchID.Value;

                            if (m.IsSetField(QuickFix.Fields.Tags.TransactTime))
                            {
                                var transactTime = m.GetDateTime(QuickFix.Fields.Tags.TransactTime);
                                ord.executionTime = transactTime;
                            }

                            if (m.IsSetNoPartyIDs())
                            {
                                int groupCount = m.GetInt(QuickFix.Fields.Tags.NoPartyIDs);

                                for (int i = 1; i <= groupCount; i++)
                                {
                                    var partyGroup = new QuickFix.FIX50SP2.ExecutionReport.NoPartyIDsGroup();
                                    m.GetGroup(i, partyGroup);   // <-- ВАЖНО!

                                    string partyId = partyGroup.GetString(QuickFix.Fields.Tags.PartyID);
                                    ord.partyId = partyId;

                                    string partySource = partyGroup.GetString(QuickFix.Fields.Tags.PartyIDSource);
                                    ord.partyIdSource = partySource;

                                    int partyRole = partyGroup.GetInt(QuickFix.Fields.Tags.PartyRole);
                                    ord.partyRole = partyRole.ToString();
                                }
                            }

                            OrdersCache.Add(ord);
                        }
                    }
                    else
                    {
                        using (var db = new MyDbContext())
                        {
                            var order = new orders();

                            // ClOrdID и OrigClOrdID
                            if (m.ExecType.getValue() == QuickFix.Fields.ExecType.CANCELED || m.ExecType.getValue() == QuickFix.Fields.ExecType.PENDING_CANCEL)
                            {
                                if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                                if (m.IsSetField(41)) order.origClOrderID = m.GetField(new QuickFix.Fields.OrigClOrdID()).getValue();
                            }
                            else if (m.ExecType.getValue() == QuickFix.Fields.ExecType.REJECTED)
                            {
                                if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                            }
                            else
                            {
                                if (m.IsSetField(11)) order.clientOrderID = m.GetField(new QuickFix.Fields.ClOrdID()).getValue();
                            }

                            if (m.IsSetField(17)) order.serial = m.GetField(new QuickFix.Fields.ExecID()).getValue();
                            if (m.IsSetField(37)) order.orderReferenceExchange = m.GetField(new QuickFix.Fields.OrderID()).getValue();

                            if (m.IsSetField(58))
                            {
                                string comment = m.GetField(new QuickFix.Fields.Text()).getValue();
                                if (Program.EXCH_CODE.Contains("KASE", StringComparison.OrdinalIgnoreCase))
                                {
                                    order.comments = Encoding.GetEncoding("windows-1251").GetString(Encoding.GetEncoding("windows-1252").GetBytes(comment));
                                }
                                else
                                {
                                    order.comments = comment;
                                }
                            }

                            if (m.IsSetField(55)) order.ticker = m.GetField(new QuickFix.Fields.Symbol()).getValue();
                            if (Program.EXCH_CODE.Contains("KASE", StringComparison.OrdinalIgnoreCase) && m.IsSetField(336))
                            {
                                order.board = m.GetString(336);
                            }
                            if (m.IsSetField(1))
                            {
                                if (Program.EXCH_CODE.Equals("AIX", StringComparison.OrdinalIgnoreCase))
                                    order.investor = m.GetField(new QuickFix.Fields.Account()).getValue();
                                else
                                    order.acc = m.GetField(new QuickFix.Fields.Account()).getValue();
                            }
                            if (m.IsSetField(528) && Program.EXCH_CODE.Equals("AIX", StringComparison.OrdinalIgnoreCase))
                                order.acc = m.GetField(new QuickFix.Fields.OrderCapacity()).Value;

                            //if (m.IsSetField(448)) order.investor = m.GetField(new QuickFix.Fields.PartyID()).getValue();

                            if (Program.EXCH_CODE.Contains("KASE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (m.IsSetField(6029)) order.currency = m.GetString(6029);
                            }
                            else if (Program.EXCH_CODE.Equals("QUIK", StringComparison.OrdinalIgnoreCase))
                            {
                                if (m.IsSetField(15)) order.currency = m.GetField(new QuickFix.Fields.Currency()).getValue();
                                if (m.IsSetField(207)) order.bloom_exchCode = m.GetField(new QuickFix.Fields.SecurityExchange()).getValue();
                                if (m.IsSetField(100)) order.sessionId = m.GetField(new QuickFix.Fields.ExDestination()).getValue();
                            }
                            else if (Program.EXCH_CODE.Equals("Bloomberg", StringComparison.OrdinalIgnoreCase))
                            {
                                if (m.IsSetField(15)) order.currency = m.GetField(new QuickFix.Fields.Currency()).getValue();
                                if (m.IsSetField(207)) order.bloom_exchCode = m.GetField(new QuickFix.Fields.SecurityExchange()).getValue();
                                //if (m.IsSetField(76)) order.contrBroker = m.GetField(new QuickFix.Fields.ExecBroker()).getValue();
                            }

                            if (m.IsSetField(54))
                                //order.direction = m.GetField(new QuickFix.Fields.Side()).getValue().ToString(); d
                                order.direction = GetSideName(m.GetChar(54));
                            if (m.IsSetField(151)) order.leavesQty = m.GetDecimal(151);
                            if (m.IsSetField(44)) order.price = m.GetDecimal(44);
                            if (m.IsSetField(38)) order.quantity = m.GetDecimal(38);
                            if (m.IsSetField(31)) order.priceDeal = m.GetDecimal(31);
                            if (m.IsSetField(32)) order.quantityDeal = (long)m.GetDecimal(32);
                            if (m.IsSetField(6)) order.priceAvg = m.GetDecimal(6);
                            if (m.IsSetField(14)) order.quantityDealTotal = (long)m.GetDecimal(14);
                            //if (m.IsSetField(126)) order.expirationDate = DateTime.Parse(m.GetString(126));
                            if (m.IsSetField(59))
                                //order.timeInForce = m.GetChar(59).ToString(); d
                                order.timeInForce = GetTimeInForceName(m.GetChar(59));
                            try
                            {
                                order.expirationDate = DateTime.ParseExact(m.ExpireDate.Value, "yyyyMMdd", CultureInfo.InvariantCulture);
                            }
                            catch { }


                            //if (m.IsSetField(921)) order.cashQty = m.GetDecimal(921);
                            if (m.IsSetField(39))
                                //order.status = m.GetChar(39).ToString(); d
                                order.status = GetOrdStatusName(m.GetChar(39));
                            if (m.IsSetField(60))
                            {
                                var field = new QuickFix.Fields.TransactTime();
                                m.GetField(field);
                                DateTime transTime = field.getValue();
                                if (Program.EXCH_CODE.Equals("KASE") || Program.EXCH_CODE.Equals("AIX", StringComparison.OrdinalIgnoreCase))
                                {
                                    order.executionTime = transTime.ToLocalTime();
                                }
                                else
                                {
                                    order.executionTime = transTime;
                                }
                                //order.ExecutionTimeStr = transTime.ToString("yyyy-MM-dd HH:mm:ss");
                            }

                            if (m.IsSetField(64)) order.settlementDate = m.GetDateOnly(64);
                            if (m.IsSetField(10500)) order.whoRemoved = m.GetString(10500);
                            if (m.IsSetField(10501))
                            {
                                try
                                {
                                    var rawTimestamp = m.GetString(10501);
                                    if (DateTime.TryParseExact(rawTimestamp, "yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                                    {
                                        order.whenRemoved = parsed;
                                    }
                                    else
                                    {
                                        order.whenRemoved = DateTime.Parse(rawTimestamp); // fallback
                                    }
                                }
                                catch
                                {
                                    order.whenRemoved = null;// DateTime.MinValue; // default fallback
                                }
                            }

                            if (m.IsSetField(529))
                            {
                                order.isMMorder = m.GetString(529) == "5" ? (byte)1 : (byte)0;
                            }
                            else
                            {
                                order.isMMorder = (byte)0;
                            }
                            try
                            {
                                if (m.NoUnderlyings.getValue() > 0)
                                {
                                    for (int i = 1; i <= m.NoUnderlyings.getValue(); i++)
                                    {
                                        var group = new QuickFix.FIX44.ExecutionReport.NoUnderlyingsGroup();
                                        m.GetGroup(i, group);

                                        if (group.IsSetField(311)) order.underlyingInstr = group.GetString(311);
                                        if (group.IsSetField(879))
                                        {
                                            var qty = new QuickFix.Fields.UnderlyingQty();
                                            group.GetField(qty);
                                            order.underlyingInstrQty = qty.getValue().ToString();
                                        }
                                    }
                                }
                            }
                            catch (Exception err) { }
                            if (m.IsSetField(236)) order.yield = m.GetDecimal(236);
                            if (m.IsSetField(40)) order.type = GetOrdTypeName(m.GetChar(40));
                            if (m.IsSetField(880)) order.TrdMatchID = m.GetString(880);

                            try
                            {
                                string raw = m.ToString().Replace('\u0001', ' ');
                                int beg = raw.IndexOf(" 57=") + 4;
                                int end = raw.IndexOf(" ", beg);
                                if (beg > 3 && end > beg)
                                {
                                    order.UserName = raw.Substring(beg, end - beg).Trim();
                                }
                            }
                            catch { }
                            if (m.IsSetField(111)) order.maxFloor = m.GetDecimal(111);

                            order.exchangeCode = Program.EXCH_CODE;
                            order.isReal = Program.ISREAL;
                            order.msgNum = int.Parse(m.Header.GetString(34));
                            order.fullMessage = m.ToJSON();
                            OrdersCache.Add(order); // замените на нужную вам структуру хранения (например, Add to List/Queue/Db)
                        }
                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }
        }


        //public void OnMessage(QuickFix.FIX50SP2.OrderCancelReject m, SessionID s)
        //{
        //    if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
        //    {
        //        if (isDebug) Console.WriteLine("Received OrderCancelReject");
        //        try
        //        {
        //            using (var db = new MyDbContext())
        //            {
        //                var order = new orders();
        //                order.clientOrderID = m.ClOrdID.Value;
        //                order.clientOrderID = m.OrigClOrdID.Value;
        //                order.status = "REJECTED";
        //                order.executionTime = DateTime.Parse(m.TransactTime.Value.ToString());
        //                string status = string.Empty;
        //                if (m.OrdStatus.Value == '1') status = "Partially filled";
        //                else if (m.OrdStatus.Value == '2') status = "Filled";
        //                else if (m.OrdStatus.Value == '4') status = "Canceled";
        //                else if (m.OrdStatus.Value == '5') status = "Replaced";
        //                else if (m.OrdStatus.Value == '8') status = "Rejected";
        //                else if (m.OrdStatus.Value == '9') status = "Suspended";
        //                else if (m.OrdStatus.Value == 'C') status = "Expired";

        //                order.comments = status + " " + m.OrdStatus.Value + " " + m.Text;

        //                db.orders.Add(order);
        //                db.SaveChanges();

                        
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
        //        }
        //    }
        //}

        public void OnMessage(QuickFix.FIX44.Reject m, SessionID s)
        {
            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
            {
                if (isDebug) Console.WriteLine("FIX44.Reject");
                try
                {
                    using (var db = new MyDbContext())
                    {
                        var order = new orders();

                        order.orderReferenceExchange = "MsgType = 3";
                        order.exchangeCode = Program.EXCH_CODE;
                        order.clientID = "RefMsgType = " + m.RefMsgType.Value;
                        order.serial = m.RefSeqNum.Value.ToString();
                        order.executionTime = DateTime.Now;
                        order.fullMessage = m.ToJSON();

                        db.orders.Add(order);
                        db.SaveChanges();
                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[FIX44.Reject] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }

        }
                
        public void OnMessage(QuickFix.FIX44.BusinessMessageReject m, SessionID s)
        {

            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
            {
                if (isDebug) Console.WriteLine("Received BusinessMessageReject");
                try
                {
                    using (var db = new MyDbContext())
                    {
                        var order = new orders();
                        //order.clientOrderID = m.ClOrdID.Value;
                        //order.clientOrderID = m.OrigClOrdID.Value;
                        //order.status = "REJECTED";
                        order.orderReferenceExchange = "MsgType = j";
                        order.status = "REJECTED";
                        order.clientID = "RefMsgType = " + m.RefMsgType.Value;
                        order.executionTime = DateTime.Now;
                        order.fullMessage = m.ToJSON();
                        order.comments = m.Text.Value;
                        order.exchangeCode = Program.EXCH_CODE;
                        order.serial = m.RefSeqNum.Value.ToString();

                        db.orders.Add(order);
                        db.SaveChanges();


                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }

        }
        public void OnMessage(QuickFix.FIX50SP2.BusinessMessageReject m, SessionID s)
        {            
            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
            {
                if (isDebug) Console.WriteLine("Received BusinessMessageReject");
                try
                {
                    using (var db = new MyDbContext())
                    {
                        int clOrdId = 0;

                        try { clOrdId = db.NewOrders.Where(r => r.msgNum == int.Parse(m.RefSeqNum.Value.ToString())).OrderByDescending(r => r.Id).Select(r => r.Id).FirstOrDefault(); } catch { }

                        var order = new orders();
                        order.msgNum = int.Parse(m.Header.GetString(34));
                        order.orderReferenceExchange = $"MsgType = {m.Header.GetString(35)}";
                        order.status = "REJECTED";
                        order.clientID = "RefMsgType = " + m.RefMsgType.Value;
                        order.executionTime = m.Header.GetDateTime(QuickFix.Fields.Tags.SendingTime);
                        order.fullMessage = m.ToJSON();
                        order.comments = m.Text.Value;
                        order.exchangeCode = Program.EXCH_CODE;
                        order.serial = m.RefSeqNum.Value.ToString();

                        if (clOrdId > 0) order.clientOrderID = clOrdId.ToString();

                        db.orders.Add(order);
                        db.SaveChanges();


                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }            
        }
        public void OnMessage(QuickFix.FIX50SP1.BusinessMessageReject m, SessionID s)
        {
            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
            {
                if (isDebug) Console.WriteLine("Received BusinessMessageReject");
                try
                {
                    using (var db = new MyDbContext())
                    {
                        int clOrdId = 0;

                        try { clOrdId = db.NewOrders.Where(r => r.msgNum == int.Parse(m.RefSeqNum.Value.ToString())).OrderByDescending(r => r.Id).Select(r => r.Id).FirstOrDefault(); } catch { }

                        var order = new orders();
                        order.msgNum = int.Parse(m.Header.GetString(34));
                        order.orderReferenceExchange = $"MsgType = {m.Header.GetString(35)}";
                        order.status = "REJECTED";
                        order.clientID = "RefMsgType = " + m.RefMsgType.Value;
                        order.executionTime = m.Header.GetDateTime(QuickFix.Fields.Tags.SendingTime);
                        order.fullMessage = m.ToJSON();
                        order.comments = m.Text.Value;
                        order.exchangeCode = Program.EXCH_CODE;
                        order.serial = m.RefSeqNum.Value.ToString();

                        if (clOrdId > 0) order.clientOrderID = clOrdId.ToString();

                        db.orders.Add(order);
                        db.SaveChanges();


                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }
        }
        public void OnMessage(QuickFix.FIX50SP2.OrderCancelReject m, SessionID s)
        {
            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
            {
                if (isDebug) Console.WriteLine("Received BusinessMessageReject");
                try
                {
                    using (var db = new MyDbContext())
                    {                        
                        var order = new orders();
                        order.msgNum = int.Parse(m.Header.GetString(34));
                        order.orderReferenceExchange = $"MsgType = {m.Header.GetString(35)}";
                        order.status = "REJECTED";
                        //order.clientID = "RefMsgType = " + m.RefMsgType.Value;
                        order.executionTime = m.Header.GetDateTime(QuickFix.Fields.Tags.SendingTime);
                        order.fullMessage = m.ToJSON();
                        string text = m.IsSetField(QuickFix.Fields.Tags.Text) ? m.GetString(QuickFix.Fields.Tags.Text) : string.Empty;
                        order.comments = $"Reason={m.CxlRejReason} {text}";
                        order.exchangeCode = Program.EXCH_CODE;
                        order.serial = m.OrderID.Value.ToString();
                        order.clientOrderID = m.ClOrdID.Value;

                        OrdersCache.Add(order);

                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }
        }
        public void OnMessage(QuickFix.FIX50SP1.OrderCancelReject m, SessionID s)
        {
            if (Program.GetValueByKey(Program.cfg, "IsWriteOrder") == "1")
            {
                if (isDebug) Console.WriteLine("Received BusinessMessageReject");
                try
                {
                    using (var db = new MyDbContext())
                    {
                        var order = new orders();
                        order.msgNum = int.Parse(m.Header.GetString(34));
                        order.orderReferenceExchange = $"MsgType = {m.Header.GetString(35)}";
                        order.status = "REJECTED";
                        //order.clientID = "RefMsgType = " + m.RefMsgType.Value;
                        order.executionTime = m.Header.GetDateTime(QuickFix.Fields.Tags.SendingTime);
                        order.fullMessage = m.ToJSON();
                        string text = m.IsSetField(QuickFix.Fields.Tags.Text) ? m.GetString(QuickFix.Fields.Tags.Text) : string.Empty;
                        order.comments = $"Reason={m.CxlRejReason} {text}";
                        order.exchangeCode = Program.EXCH_CODE;
                        order.serial = m.OrderID.Value.ToString();
                        order.clientOrderID = m.ClOrdID.Value;

                        OrdersCache.Add(order);

                    }
                }
                catch (Exception e)
                {
                    DailyLogger.Log($"[ExecutionReport] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
                }
            }
        }
        public void OnMessage(QuickFix.FIX44.TradeCaptureReport m, SessionID s)
        {
            try
            {
                using (var db = new MyDbContext())
                {
                    tradeCapture tc = new tradeCapture();
                    tc.TradeReportID = m.TradeReportID.Value;
                    if (m.IsSetField(818)) tc.SecondaryTradeReportID = m.SecondaryTradeReportID.Value;
                    tc.ExecType = m.ExecType.Value.ToString();
                    tc.ExecID = m.ExecID.Value;
                    tc.LastQty = m.LastQty.Value.ToString();
                    tc.LastPx = m.LastPx.Value.ToString();
                    if (m.IsSetField(1056)) tc.CalculatedCcyLastQty = m.GetString(1056);
                    tc.TradeDate = m.TradeDate.Value;
                    tc.TransactTime=m.TransactTime.Value.ToString();
                    if (m.IsSetField(64)) tc.SettlDate = m.SettlDate.Value;
                    if (m.IsSetField(5459)) tc.OptionSettlType=m.GetString(5459);
                    //если есть группа NoSides
                    if(m.IsSetNoSides())
                    {
                        int sidesCount = m.NoSides.Value; //m.GetInt(552);
                        for (int i = 0; i < sidesCount; i++)
                        {
                            // Получаем группу по индексу
                            QuickFix.FIX44.TradeCaptureReport.NoSidesGroup sideGroup =
                                new QuickFix.FIX44.TradeCaptureReport.NoSidesGroup();
                            m.GetGroup(i, sideGroup);
                            // Читаем поля внутри группы
                            if (sideGroup.IsSetSide())
                            {
                                // Side (54) — char
                                var sideField = new Side();
                                sideGroup.GetField(sideField);
                                tc.Side = sideField.Value.ToString();
                            }
                            else if (sideGroup.IsSetOrderID())
                            {
                                var orderIdField = new OrderID();
                                sideGroup.GetField(orderIdField);
                                tc.OrderID = orderIdField.Value.ToString();
                            }
                            else if (sideGroup.IsSetClOrdID())
                            {
                                var clOrderIdField = new ClOrdID();
                                sideGroup.GetField(clOrderIdField);
                                tc.ClOrdID = clOrderIdField.Value.ToString();
                            }
                            else if (sideGroup.IsSetSecondaryClOrdID())
                            {
                                var secClOrderIdField = new SecondaryClOrdID();
                                sideGroup.GetField(secClOrderIdField);
                                tc.SecondaryClOrdID = secClOrderIdField.Value.ToString();
                            }
                          
                        }
                        

                    }
                    if (m.IsSetField(Tags.LastPx))
                    {
                        tc.Price = m.GetDecimal(Tags.LastPx).ToString();
                    }
                    if (m.IsSetField(640))
                    {
                        tc.Price2 = m.GetDecimal(640).ToString();
                    }
                    if (m.IsSetField(Tags.PriceType))
                    {
                        tc.PriceType = m.GetInt(Tags.PriceType).ToString();
                    }
                    if (m.IsSetField(5155))
                    {
                        tc.InstitutionID = m.GetString(5155);
                    }
                    if (m.IsSetField(6029))
                    {
                        tc.CurrencyCode = m.GetString(6029);
                    }
                    if (m.IsSetField(9580))
                    {
                        tc.ParentID = m.GetString(9580);
                    }
                    
                    db.TradeCapture.Add(tc);
                    db.SaveChanges();

                }

            }
            catch(Exception ex)
            {
                Console.WriteLine("Ошибка TradeCaptureReport - " + ex.Message);
            }
        }

        private string GetSideName(char side)
{
    return side switch
    {
        '1' => "BUY",
        '2' => "SELL",
        '3' => "BUY MINUS",
        '4' => "SELL PLUS",
        '5' => "SELL SHORT",
        '6' => "SELL SHORT EXEMPT",
        '7' => "UNDISCLOSED",
        '8' => "CROSS",
        '9' => "CROSS SHORT",
        'A' => "CROSS SHORT EXEMPT",
        'B' => "AS DEFINED",
        'C' => "OPPOSITE",
        'D' => "SUBSCRIBE",
        'E' => "REDEEM",
        'F' => "LEND",
        'G' => "BORROW",
        _ => $"UNKNOWN({side})"
    };
}
        private string GetOrdTypeName(char type)
        {
            return type switch
            {
                '1' => "MARKET",
                '2' => "LIMIT",
                '3' => "STOP",
                '4' => "STOP LIMIT",
                '5' => "MARKET ON CLOSE",
                '6' => "WITH OR WITHOUT",
                '7' => "LIMIT OR BETTER",
                '8' => "LIMIT WITH OR WITHOUT",
                '9' => "ON BASIS",
                'A' => "ON CLOSE",
                'B' => "LIMIT ON CLOSE",
                'C' => "FOREX MARKET",
                'D' => "PREVIOUSLY QUOTED",
                'E' => "PREVIOUSLY INDICATED",
                'F' => "FOREX LIMIT",
                'G' => "FOREX SWAP",
                'H' => "FOREX PREVIOUSLY QUOTED",
                'I' => "FUNARI",
                'J' => "MARKET IF TOUCHED",
                'K' => "MARKET WITH LEFTOVER AS LIMIT",
                'L' => "PREVIOUS FUND VALUATION POINT",
                'M' => "NEXT FUND VALUATION POINT",
                'P' => "PEGGED",
                'n' => "NEGOTIATED",
                'o' => "EXPANDED_LIQUIDITY_POOL",
                _ => $"UNKNOWN({type})"
            };
        }


        private string GetTimeInForceName(char tif)
        {
            return tif switch
            {
                '0' => "DAY",
                '1' => "GOOD TILL CANCEL",
                '2' => "AT THE OPENING",
                '3' => "IMMEDIATE OR CANCEL",
                '4' => "FILL OR KILL",
                '5' => "GOOD TILL CROSSING",
                '6' => "GOOD TILL DATE",
                '7' => "AT THE CLOSE",
                'x' => "during the extended trading session",
                _ => $"UNKNOWN({tif})"
            };
        }
        private string GetOrdStatusName(char status)
        {
            return status switch
            {
                '0' => "NEW",
                '1' => "PARTIALLY FILLED",
                '2' => "FILLED",
                '3' => "DONE FOR DAY",
                '4' => "CANCELED",
                '5' => "REPLACED",
                '6' => "PENDING CANCEL",
                '7' => "STOPPED",
                '8' => "REJECTED",
                '9' => "SUSPENDED",
                'A' => "PENDING NEW",
                'B' => "CALCULATED",
                'C' => "EXPIRED",
                'D' => "ACCEPTED FOR BIDDING",
                'E' => "PENDING REPLACE",
                _ => $"UNKNOWN({status})"
            };
        }

        public static ConcurrentBag<orders> OrdersCache = new ConcurrentBag<orders>();
        
        
        public void OnMessage(QuickFix.FIX44.MarketDataIncrementalRefresh m, SessionID s)
        {
            if(isDebug) Console.WriteLine("Received MarketDataIncrementalRefresh");
            try { 
                using (var db = new MyDbContext())
            {
                quotesSimple quote = new quotesSimple();
                string symbol = null;

                for (int i = 1; i <= m.NoMDEntries.getValue(); i++)
                {
                    var group = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();
                    m.GetGroup(i, group);

                    string mdEntryType = group.GetField(new QuickFix.Fields.MDEntryType()).getValue().ToString();
                    decimal? price = null;
                    decimal? size = null;

                    // Получаем цену
                    if (group.IsSetField(QuickFix.Fields.MDEntryPx.TAG))
                    {
                        var mdEntryPx = new QuickFix.Fields.MDEntryPx();
                        group.GetField(mdEntryPx);
                        price = mdEntryPx.getValue();
                    }

                    // Получаем количество
                    if (group.IsSetField(QuickFix.Fields.MDEntrySize.TAG))
                    {
                        var mdEntrySize = new QuickFix.Fields.MDEntrySize();
                        group.GetField(mdEntrySize);
                        size = mdEntrySize.getValue();
                    }

                    // Получаем символ
                    if (group.IsSetField(QuickFix.Fields.Symbol.TAG))
                    {
                        var symbolField = new QuickFix.Fields.Symbol();
                        group.GetField(symbolField);
                        symbol = symbolField.getValue();
                    }

                    if (!price.HasValue)
                        continue; // Если нет цены, пропускаем запись

                    switch (mdEntryType)
                    {
                        case "0": // Bid
                            quote.bid = price.Value;
                            quote.bidQuantity = size ?? 0;
                            break;

                        case "1": // Offer (Ask)
                            quote.ask = price.Value;
                            quote.askQuantity = size ?? 0;
                            break;

                        case "2": // Last Price (Trade)
                            quote.lastTrade = price.Value;
                            //quote.lastTrade = size ?? 0;
                            break;

                        //case "4": // Opening price
                        //    quote.openPrice = price.Value;
                        //    break;

                        //case "5": // Closing price
                        //    quote.closePrice = price.Value;
                        //    break;

                        //case "B": // Trade volume отдельно
                        //    quote.totalVolume = size ?? 0;
                        //    break;

                        default:
                            if(isDebug) Console.WriteLine($"Unknown MDEntryType: {mdEntryType}");
                            break;
                    }
                }

                quote.isReal = isDebug ? (byte)0 : (byte)1;
                quote.exchangeCode = Program.EXCH_CODE; //"Exante";
                quote.ticker = symbol;
                quote.msgNum = m.Header.GetString(34);
                quote.sendingTime = m.Header.GetString(52);
                //db.quotesSimple.Add(quote);
                //db.SaveChanges();
                quotesSimples.Add(quote);
            }
             }
            catch(Exception e) {
                DailyLogger.Log($"[MarketDataIncrementalRefresh] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
            }
        }
        
        
        public static ConcurrentBag<quotesSimple> quotesSimples = new ConcurrentBag<quotesSimple>();
        
        
        public void OnMessage(QuickFix.FIX44.MarketDataSnapshotFullRefresh m, SessionID sy)
        {
            if(isDebug) Console.WriteLine("Received MarketDataSnapshotFullRefresh");
            try { 
                using (var db = new MyDbContext())
            {

                // Получаем символ инструмента
                string symbol = null;
                if (m.IsSetField(QuickFix.Fields.Symbol.TAG))
                {
                    var symbolField = new QuickFix.Fields.Symbol();
                    m.GetField(symbolField);
                    symbol = symbolField.getValue();
                }

                if (string.IsNullOrEmpty(symbol))
                {
                    if(isDebug) Console.WriteLine("Symbol not found in Snapshot. Skipping...");
                    return;
                }

                var quote = new quotesSimple
                {
                    isReal = isDebug ? (byte)0 : (byte)1,
                    exchangeCode = Program.EXCH_CODE, //"Exante",
                    ticker = symbol,
                    msgNum = m.Header.GetString(34),
                    sendingTime = m.Header.GetString(52)
                    //INPDATE = msg.Header.
                };

                for (int i = 1; i <= m.NoMDEntries.getValue(); i++)
                {
                    var group = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
                    m.GetGroup(i, group);

                    string mdEntryType = group.GetField(new QuickFix.Fields.MDEntryType()).getValue().ToString();
                    decimal? price = null;
                    decimal? size = null;

                    if (group.IsSetField(QuickFix.Fields.MDEntryPx.TAG))
                    {
                        var mdEntryPx = new QuickFix.Fields.MDEntryPx();
                        group.GetField(mdEntryPx);
                        price = mdEntryPx.getValue();
                    }

                    if (group.IsSetField(QuickFix.Fields.MDEntrySize.TAG))
                    {
                        var mdEntrySize = new QuickFix.Fields.MDEntrySize();
                        group.GetField(mdEntrySize);
                        size = mdEntrySize.getValue();
                    }

                    if (!price.HasValue)
                        continue;

                    switch (mdEntryType)
                    {
                        case "0": // Bid
                            quote.bid = price.Value;
                            quote.bidQuantity = size ?? 0;
                            break;

                        case "1": // Ask
                            quote.ask = price.Value;
                            quote.askQuantity = size ?? 0;
                            break;

                        case "2": // Last
                            quote.lastTrade = price.Value;
                            //quote.askQuantity = size ?? 0;
                            break;
                        default:
                            if(isDebug) Console.WriteLine($"Skipping unknown MDEntryType: {mdEntryType}");
                            break;
                    }
                }
                
                //db.quotesSimple.Add(quote);
                //db.SaveChanges();
                quotesSimples.Add(quote);
            }
             }
            catch(Exception e) {
                DailyLogger.Log($"[MarketDataSnapshotFullRefresh] OnMessage : {e.Message} " + JsonConvert.SerializeObject(m));
            }
        }

        

        public void OnMessage(QuickFix.FIX44.MarketDataRequestReject m, SessionID s)
        {
            
            if(isDebug) Console.WriteLine("Received MarketDataRequestReject");
        }

        public void OnMessage(QuickFix.FIX44.OrderCancelReject m, SessionID s)
        {
            if(isDebug) Console.WriteLine("Received OrderCancelReject");
        }
        #endregion


        public void Run()
        {
            
            //if (this.MyInitiator is null || !this.MyInitiator.IsLoggedOn)
            //    throw new ApplicationException("Somehow this.MyInitiator is not set");
            //int i = 0;
            while (true)   
            {
                if (this.MyInitiator is not null && this.MyInitiator.IsLoggedOn)
                    checkNewOrders();                
            }
            return;
            //if (this.MyInitiator is null)
            //    throw new ApplicationException("Somehow this.MyInitiator is not set");

            while (true)
            {
                try
                {
                    char action = QueryAction();
                    if (action == '1')
                        QueryEnterOrder();
                    else if (action == '2')
                        QueryCancelOrder();
                    else if (action == '3')
                        QueryReplaceOrder();
                    else if (action == '4')
                        QueryMarketDataRequest();
                    else if (action == 'g')
                    {
                        if (this.MyInitiator.IsStopped)
                        {
                            if(isDebug) Console.WriteLine("Restarting initiator...");
                            this.MyInitiator.Start();
                        }
                        else
                            if(isDebug) Console.WriteLine("Already started.");
                    }
                    else if (action == 'x')
                    {
                        if (this.MyInitiator.IsStopped)
                            if(isDebug) Console.WriteLine("Already stopped.");
                        else
                        {
                            if(isDebug) Console.WriteLine("Stopping initiator...");
                            this.MyInitiator.Stop();
                        }
                    }
                    else if (action == 'q' || action == 'Q')
                        break;
                }
                catch (Exception e)
                {
                    if(isDebug) Console.WriteLine("Message Not Sent: " + e.Message);
                    if(isDebug) Console.WriteLine("StackTrace: " + e.StackTrace);
                }
            }
            if(isDebug) Console.WriteLine("Program shutdown.");
        }

        private void SendMessage(Message m)
        {
            if (_session is not null)
                _session.Send(m);
            else
            {
                // This probably won't ever happen.
                if(isDebug) Console.WriteLine("Can't send message: session not created.");
            }
        }

        private static string ReadCommand() {
            string? inp = Console.ReadLine();
            if (inp is null)
                throw new ApplicationException("Input no longer available");
            return inp.Trim();
        }

        private char QueryAction()
        {
            // Commands 'g' and 'x' are intentionally hidden.
            if(isDebug) Console.Write("\n"
                + "1) Enter Order\n"
                + "2) Cancel Order\n"
                + "3) Replace Order\n"
                + "4) Market data test\n"
                + "Q) Quit\n"
                + "Action: "
            );

            HashSet<string> validActions = new HashSet<string>("1,2,3,4,q,Q,g,x".Split(','));

            string cmd = ReadCommand();
            if (cmd.Length != 1 || validActions.Contains(cmd) == false)
                throw new System.Exception("Invalid action");

            return cmd.ToCharArray()[0];
        }

        private void QueryEnterOrder()
        {
            if(isDebug) Console.WriteLine("\nNewOrderSingle");

            QuickFix.FIX44.NewOrderSingle m = QueryNewOrderSingle44();

            if (m is not null && QueryConfirm("Send order"))
            {
                m.Header.GetString(Tags.BeginString);

                SendMessage(m);
            }
        }

        private void QueryCancelOrder()
        {
            if(isDebug) Console.WriteLine("\nOrderCancelRequest");

            QuickFix.FIX44.OrderCancelRequest m = QueryOrderCancelRequest44();

            if (m != null && QueryConfirm("Cancel order"))
                SendMessage(m);
        }

        private void QueryReplaceOrder()
        {
            if(isDebug) Console.WriteLine("\nCancelReplaceRequest");

            QuickFix.FIX44.OrderCancelReplaceRequest m = QueryCancelReplaceRequest44();

            if (m != null && QueryConfirm("Send replace"))
                SendMessage(m);
        }

        private void QueryMarketDataRequest()
        {
            if(isDebug) Console.WriteLine("\nMarketDataRequest");

            QuickFix.FIX44.MarketDataRequest m = QueryMarketDataRequest44();

            if (QueryConfirm("Send market data request"))
                SendMessage(m);
        }
        private static Random random = new Random();
        private static string NextRef()
        {
            byte[] buffer = new byte[8]; // 8 байт = 64 бита
            random.NextBytes(buffer);
            long longValue = BitConverter.ToInt64(buffer, 0);
            return Math.Abs(longValue).ToString(); // убрать знак (аналог Long.MAX_VALUE)
        }
        public void SubscribeInstrument(string symbol, string exanteId, SessionID sessionID)
        {
            var marketDataRequest = new QuickFix.FIX44.MarketDataRequest();

            marketDataRequest.Set(new MDReqID(NextRef().ToString())); // 262 - Уникальный ID запроса
            marketDataRequest.Set(new SubscriptionRequestType(SubscriptionRequestType.SNAPSHOT_PLUS_UPDATES)); // 263 - 1 = Snapshot + Updates
            marketDataRequest.Set(new MarketDepth(0)); // 264 - 0 = Top of book
            marketDataRequest.Set(new MDUpdateType(0)); // 265 - 0 = Full Refresh

            // Сначала добавить группу инструментов (NoRelatedSym 146=1)
            var symbolGroup = new Group(146, 55);
            symbolGroup.SetField(new Symbol(exanteId));           // 55 - AAPL.NASDAQ
            symbolGroup.SetField(new SecurityID(exanteId));        // 48 - AAPL.NASDAQ
            symbolGroup.SetField(new SecurityIDSource("111"));     // 22 - Внешний код
            marketDataRequest.AddGroup(symbolGroup);

            // Теперь группы типов цен (NoMDEntryTypes 267=6)
            var entryTypes = new List<char>
    {
        MDEntryType.BID,            // 269=0
        MDEntryType.OFFER,          // 269=1
        MDEntryType.OPENING_PRICE,  // 269=4
        MDEntryType.TRADE,          // 269=2
        'B',                        // 269=B - Book Depth (Bid/Ask Imbalance)
        MDEntryType.CLOSING_PRICE   // 269=5
    };

            foreach (var entryType in entryTypes)
            {
                var entryTypeGroup = new Group(267, 269);
                entryTypeGroup.SetField(new MDEntryType(entryType));
                marketDataRequest.AddGroup(entryTypeGroup);
            }

            if(isDebug) Console.WriteLine($"[Subscribe] Подписка на инструмент: {symbol} ({exanteId})");

            // Отправляем
            Session.SendToTarget(marketDataRequest, sessionID);
        }
        private string SendMarketDataRequest(SessionID sessionID, instrsView instr)
        {
            string requestId = Guid.NewGuid().ToString();
            var request = new QuickFix.FIX44.MarketDataRequest(
                new MDReqID(requestId),
                new SubscriptionRequestType(SubscriptionRequestType.SNAPSHOT_PLUS_UPDATES),
                new MarketDepth(1) // Глубина стакана
            );

            request.Set(new MarketDepth(0)); // 264 = 0
            request.Set(new MDUpdateType(0)); // 265 = Full Refresh

            // Типы запрашиваемых данных (Bid + Ask)
            var entryTypesGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            entryTypesGroup.Set(new MDEntryType(MDEntryType.BID));  // Bid
            request.AddGroup(entryTypesGroup);

            entryTypesGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            entryTypesGroup.Set(new MDEntryType(MDEntryType.OFFER)); // Ask
            request.AddGroup(entryTypesGroup);

            entryTypesGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            entryTypesGroup.Set(new MDEntryType(MDEntryType.TRADE)); // Ask
            request.AddGroup(entryTypesGroup);


            // Связанные инструменты (Symbols)
            var symbolGroup = new Group(146, 55, new int[] { 55, 48, 22 }); // порядок полей
            symbolGroup.SetField(new Symbol(instr.symbol));        // 55
            symbolGroup.SetField(new SecurityID(instr.codeMubasher)); // 48
            symbolGroup.SetField(new SecurityIDSource("111"));      // 22
            request.AddGroup(symbolGroup);

            // Отправляем запрос
            if (Session.SendToTarget(request, sessionID))
            {
                if(isDebug) Console.WriteLine($"MarketDataRequest sent successfully for instrument: {instr.symbol}");
            }
            else
            {
                if(isDebug) Console.WriteLine("Failed to send MarketDataRequest for instrument: {instr.symbol}");
            }

            return requestId;
        }

        private string SendMarketDataRequestBad(SessionID sessionID, instrsView instr)
        {
            string requestId = Guid.NewGuid().ToString();
            var request = new QuickFix.FIX44.MarketDataRequest(
                new MDReqID(requestId), // Уникальный ID запроса
                new SubscriptionRequestType(SubscriptionRequestType.SNAPSHOT_PLUS_UPDATES),
                new MarketDepth(1) // Глубина стакана (1 — только лучший Bid/Ask)
            );
            request.Set(new MDReqID(NextRef().ToString())); // 262 - Уникальный ID запроса
            request.Set(new SubscriptionRequestType(SubscriptionRequestType.SNAPSHOT_PLUS_UPDATES)); // 263 - 1 = Snapshot + Updates
            request.Set(new MarketDepth(0)); // 264 - 0 = Top of book
            request.Set(new MDUpdateType(0)); // 265 - 0 = Full Refresh

            // Типы запрашиваемых данных (Bid/Ask)
            var entryTypesGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            entryTypesGroup.Set(new MDEntryType(MDEntryType.BID));  // Bid
            request.AddGroup(entryTypesGroup);

            entryTypesGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            entryTypesGroup.Set(new MDEntryType(MDEntryType.OFFER)); // Ask
            request.AddGroup(entryTypesGroup);
            //8=FIX.4.49=18635=V34=249=MOY4338_FEED_UAT52=20250417-16:01:46.07356=EXANTE_FEED_UAT262=1726263=1264=0265=0146=155=AAPL.NASDAQ48=AAPL.NASDAQ22=111267=6269=0269=1269=4269=2269=B269=510=034
            // Инструменты (например EUR/USD или BTC/USD)
            var instruments = new[] { instr.symbol };

            //foreach (var symbol in instruments)
            //{
            //    var symbolGroup = new QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup();
            //    symbolGroup.Set(new Symbol(symbol));
            //    request.AddGroup(symbolGroup);
            //}
            //var symbolGroup2 = new Group(146, 55);
            //symbolGroup2.SetField(new Symbol("AAPL.NASDAQ"));           // 55 - AAPL.NASDAQ
            //symbolGroup2.SetField(new SecurityID("AAPL.NASDAQ"));        // 48 - AAPL.NASDAQ
            //symbolGroup2.SetField(new SecurityIDSource("111"));     // 22 - Внешний код
            //request.AddGroup(symbolGroup2);

            var symbolGroup = new Group(146, 55, new int[] { 55, 48, 22 }); // <<< ПРАВИЛЬНЫЙ ПОРЯДОК
            symbolGroup.SetField(new QuickFix.Fields.Symbol(instr.symbol));        // 55
            symbolGroup.SetField(new QuickFix.Fields.SecurityID(instr.codeMubasher));     // 48
            symbolGroup.SetField(new QuickFix.Fields.SecurityIDSource("111"));  // 22
            request.AddGroup(symbolGroup);
            // Отправляем запрос
            if (Session.SendToTarget(request, sessionID))
            {
                if(isDebug) Console.WriteLine($"MarketDataRequest sent successfully for instruments: {string.Join(", ", instruments)}");
            }
            else
            {
                if(isDebug) Console.WriteLine("Failed to send MarketDataRequest");
            }
            return requestId;
        }
        private void SendMarketDataUnsubscribe(SessionID sessionID, instrsView instr)
        {
            // Создаем запрос на маркет-дату (MarketDataRequest) для отмены подписки
            var request = new QuickFix.FIX44.MarketDataRequest(
                new MDReqID(instr.requestId), // ❗ Используем старый (существующий) RequestId для отписки
                new SubscriptionRequestType(SubscriptionRequestType.DISABLE_PREVIOUS_SNAPSHOT_PLUS_UPDATE_REQUEST), // 263 = 2 (Disable previous snapshot + cancel updates)
                new MarketDepth(0) // 264 - глубина не нужна при отмене
            );

            request.Set(new MDUpdateType(0)); // 265 - Full Refresh (обычно 0)

            // Запрашиваем типы данных, от которых нужно отписаться (как в примере: BID, OFFER, TRADE)
            var entryTypes = new[]
            {
        MDEntryType.BID,
        MDEntryType.OFFER,
        MDEntryType.TRADE
    };

            foreach (var entryType in entryTypes)
            {
                var entryTypesGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
                entryTypesGroup.Set(new MDEntryType(entryType));
                request.AddGroup(entryTypesGroup);
            }

            // Указываем инструмент
            var symbolGroup = new Group(146, 55, new int[] { 55, 48, 22 });
            symbolGroup.SetField(new QuickFix.Fields.Symbol(instr.symbol));             // 55
            symbolGroup.SetField(new QuickFix.Fields.SecurityID(instr.codeMubasher));    // 48
            symbolGroup.SetField(new QuickFix.Fields.SecurityIDSource("111"));           // 22
            request.AddGroup(symbolGroup);

            // Отправляем запрос на отписку
            if (Session.SendToTarget(request, sessionID))
            {
                if(isDebug) Console.WriteLine($"MarketDataRequest unsubscribe sent successfully for instrument: {instr.symbol}");
            }
            else
            {
                if(isDebug) Console.WriteLine("Failed to send MarketDataRequest unsubscribe");
            }
        }

        private bool QueryConfirm(string query)
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.WriteLine(query + "?: ");
            string line = ReadCommand();
            return (line[0].Equals('y') || line[0].Equals('Y'));
        }
        private void cancelOrder(NewOrders ord)
        {
            try
            {
                if (Program.EXCH_CODE == "ITS")
                {
                    Side side;
                    if (ord.Direction == "CANCEL_BUY") side = new Side(Side.BUY);
                    else if (ord.Direction == "CANCEL_SELL") side = new Side(Side.SELL);
                    else
                    {
                        //здесь нужно поставить логирование
                        return;
                    }
                    QuickFix.FIX50SP2.OrderCancelRequest req = new QuickFix.FIX50SP2.OrderCancelRequest();
                    if(!string.IsNullOrEmpty(ord.OrigClOrderID)) req.OrigClOrdID = new OrigClOrdID(ord.OrigClOrderID.ToString());
                    req.ClOrdID = new ClOrdID(ord.Id.ToString());
                    req.TransactTime = new TransactTime(DateTime.Now);
                    if(!string.IsNullOrEmpty(ord.exDestination))
                    {
                        //ExDestination
                        var exDestination = new StringField(100,ord.exDestination);
                        req.SetField(exDestination);
                    }
                    req.SecurityID = new SecurityID(ord.Ticker);
                    req.Side = side;
                    req.Account = new Account(ord.Acc);
                    var partyIdGroup = new QuickFix.FIX50SP2.NewOrderSingle.NoPartyIDsGroup();
                    if (ord.Acc == "STDICLIENT")
                    {
                        partyIdGroup.SetField(new PartyID("387"));
                        partyIdGroup.SetField(new PartyIDSource(char.Parse("D")));
                        partyIdGroup.SetField(new PartyRole(int.Parse("1")));
                        req.AddGroup(partyIdGroup);
                    }
                    else if (ord.Acc == "STDIOWN")
                    {
                        partyIdGroup.SetField(new PartyID("387"));
                        partyIdGroup.SetField(new PartyIDSource(char.Parse("D")));
                        partyIdGroup.SetField(new PartyRole(int.Parse("1")));
                        req.AddGroup(partyIdGroup);
                    }
                    partyIdGroup.SetField(new PartyID(ord.SenderSubID));
                    partyIdGroup.SetField(new PartyIDSource(char.Parse(ord.PartyIDSource)));
                    partyIdGroup.SetField(new PartyRole(int.Parse(ord.PartyRole)));
                    req.AddGroup(partyIdGroup);

                    req.Header.GetString(Tags.BeginString);

                    SendMessage(req);
                }
                else if (Program.EXCH_CODE == "AIX_SP1")
                {
                    Side side;
                    if (ord.Direction == "CANCEL_BUY") side = new Side(Side.BUY);
                    else if (ord.Direction == "CANCEL_SELL") side = new Side(Side.SELL);
                    else
                    {
                        //здесь нужно поставить логирование
                        return;
                    }
                    QuickFix.FIX50SP1.OrderCancelRequest req = new QuickFix.FIX50SP1.OrderCancelRequest();
                    if (!string.IsNullOrEmpty(ord.OrigClOrderID)) req.OrigClOrdID = new OrigClOrdID(ord.OrigClOrderID.ToString());
                    req.ClOrdID = new ClOrdID(ord.Id.ToString());
                    req.TransactTime = new TransactTime(DateTime.Now);
                    req.Symbol = new Symbol(ord.Ticker);
                    req.Side = side;
                    req.Account = new Account(ord.Acc);
                    
                    var partyIdGroup = new QuickFix.FIX50SP1.OrderCancelRequest.NoPartyIDsGroup();                                        
                    partyIdGroup.SetField(new PartyID(ord.Investor));
                    partyIdGroup.SetField(new PartyIDSource(char.Parse(ord.PartyIDSource)));
                    partyIdGroup.SetField(new PartyRole(int.Parse(ord.PartyRole)));
                    req.AddGroup(partyIdGroup);                    

                    req.Header.GetString(Tags.BeginString);

                    SendMessage(req);
                }
                else
                {
                    Side side;
                    if (ord.Direction == "CANCEL_BUY") side = new Side(Side.BUY);
                    else if (ord.Direction == "CANCEL_SELL") side = new Side(Side.SELL);
                    else
                    {
                        //здесь нужно поставить логирование
                        return;
                    }
                    QuickFix.FIX44.OrderCancelRequest orderCancelRequest = new QuickFix.FIX44.OrderCancelRequest(
                    new OrigClOrdID(ord.OrigClOrderID.ToString()), //new OrigClOrdID("NONE"), //
                    new ClOrdID(ord.Id.ToString()),
                    new Symbol(ord.Ticker),
                    side,
                    new TransactTime(DateTime.Now));

                    //orderCancelRequest.Set(new OrderID(ord.OrigClOrderID.ToString()));
                    orderCancelRequest.Set(new OrderQty(int.Parse(ord.Quantity.Value.ToString())));
                    SendMessage(orderCancelRequest);
                }
                
                
            }
            catch 
            {
                //здесь нужно поставить логирование
            }

        }
        //проверка наличия новых необработанных приказов
        public void checkNewOrders()
        {
            try
            {
                MyDbContext db = new MyDbContext();
                //var rz1 = db.newOrders.Where(r => r.Processed_Status is null);

                var rz = db.NewOrders.Where(r => string.IsNullOrEmpty(r.Processed_Status)
                    && r.ExchangeCode == Program.EXCH_CODE && ((r.ExpirationDate == null && r.TimeInForce != "DAY") || r.ExpirationDate >= DateTime.UtcNow.Date) //берем только те, которые еще не просрочились 
                    ).OrderBy(r=>r.Id).Take(portionSendOrder).ToList();
                
                DateTime startTime = DateTime.Now;

                //List<Int32> lstIdSended = new List<Int32>(); 
                foreach ( var r in rz)
                {

                    OrdType ordType = new OrdType();
                    if (r.Type.ToUpper() == "MARKET") ordType = new OrdType(OrdType.MARKET);
                    else if (r.Type.ToUpper() == "LIMIT") ordType = new OrdType(OrdType.LIMIT);
                    else
                    {
                        //здесь нужно поставить логирование
                        continue;
                    }
                    char s = ' ';
                    if(r.Direction.ToUpper()=="BUY") s= Side.BUY;
                    else if (r.Direction.ToUpper() == "SELL") s = Side.SELL;
                    else if (r.Direction.ToUpper() == "CANCEL_BUY" || r.Direction.ToUpper() == "CANCEL_SELL")
                    {
                        cancelOrder(r);
                        r.Processed_Status = "send";
                        continue;
                    }
                    else
                    {
                        //здесь нужно ставить логирование
                        continue;
                    }
                    if (Program.EXCH_CODE == "AIX")
                    {

                      QuickFix.FIX50SP2.NewOrderSingle ord1 = new QuickFix.FIX50SP2.NewOrderSingle(
                          new ClOrdID(r.Id.ToString()),
                          new Side(s),
                          new TransactTime(DateTime.Now),
                          ordType
                          );

                        ord1.Set(new Symbol(r.Ticker));
                        ord1.Set(new HandlInst('1'));
                        ord1.Set(new OrderQty(int.Parse(r.Quantity.Value.ToString())));

                        s = ' ';
                        if (r.TimeInForce.ToUpper() == "DAY") s = TimeInForce.DAY;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE") s = TimeInForce.GOOD_TILL_DATE;
                        else if (r.TimeInForce.ToUpper() == "IMMEDIATE_OR_CANCEL") s = TimeInForce.IMMEDIATE_OR_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CANCEL") s = TimeInForce.GOOD_TILL_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CROSSING") s = TimeInForce.GOOD_TILL_CROSSING;
                        else if (r.TimeInForce.ToUpper() == "FILL_OR_KILL") s = TimeInForce.FILL_OR_KILL;
                        else
                        {
                            //здесь нужно ставить логирование
                            continue;
                        }
                        ord1.Set(new TimeInForce(s));
                        if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE")
                            ord1.Set(new ExpireDate(r.ExpirationDate.Value.ToString("yyyyMMdd")));

                        if (ordType.Value == OrdType.LIMIT || ordType.Value == OrdType.STOP_LIMIT)
                            ord1.Set(new Price(r.Price.Value));

                        if(r.MaxFloor is not null)
                        {
                            if(r.MaxFloor>0) ord1.Set(new MaxFloor(r.MaxFloor.Value));
                        }

                        ord1.Set(new Account(r.Investor));
                        ord1.Set(new OrderCapacity(r.Acc));
                        ord1.Header.GetString(Tags.BeginString);

                        SendMessage(ord1);
                        r.Processed_Status = "send";

                    }
                    else if (Program.EXCH_CODE == "AIX_SP1")
                    {

                        QuickFix.FIX50SP1.NewOrderSingle ord1 = new QuickFix.FIX50SP1.NewOrderSingle(
                            new ClOrdID(r.Id.ToString()),
                            new Side(s),
                            new TransactTime(DateTime.Now),
                            ordType
                            );

                        ord1.Set(new Symbol(r.Ticker));
                        ord1.Set(new HandlInst('1'));

                        ord1.Set(new OrderQty(Convert.ToInt32(r.Quantity.Value, CultureInfo.InvariantCulture)));

                        s = ' ';
                        if (r.TimeInForce.ToUpper() == "DAY") s = TimeInForce.DAY;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE") s = TimeInForce.GOOD_TILL_DATE;
                        else if (r.TimeInForce.ToUpper() == "IMMEDIATE_OR_CANCEL") s = TimeInForce.IMMEDIATE_OR_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CANCEL") s = TimeInForce.GOOD_TILL_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CROSSING") s = TimeInForce.GOOD_TILL_CROSSING;
                        else if (r.TimeInForce.ToUpper() == "FILL_OR_KILL") s = TimeInForce.FILL_OR_KILL;
                        else
                        {
                            //здесь нужно ставить логирование
                            continue;
                        }
                        ord1.Set(new TimeInForce(s));
                        if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE")
                            ord1.Set(new ExpireDate(r.ExpirationDate.Value.ToString("yyyyMMdd")));

                        if (ordType.Value == OrdType.LIMIT || ordType.Value == OrdType.STOP_LIMIT)
                        {
                            ord1.Set(new Price(Convert.ToInt32(r.Price.Value, CultureInfo.InvariantCulture)));
                        }

                        if (r.MaxFloor is not null)
                        {
                            if (r.MaxFloor > 0) ord1.Set(new DisplayQty(r.MaxFloor.Value));
                        }

                        ord1.Set(new Account(r.Acc));
                        //ord1.Set(new OrderCapacity(r.Acc)); //r.Investor

                        ord1.SetField(new NoPartyIDs(3));                                        

                        var partyIdGroup = new QuickFix.FIX50SP1.NewOrderSingle.NoPartyIDsGroup();                        
                        partyIdGroup.SetField(new PartyID(r.Investor));
                        partyIdGroup.SetField(new PartyIDSource(char.Parse(r.PartyIDSource)));
                        partyIdGroup.SetField(new PartyRole(int.Parse(r.PartyRole)));
                        ord1.AddGroup(partyIdGroup);

                        ord1.Header.GetString(Tags.BeginString);

                        SendMessage(ord1);
                        r.Processed_Status = "send";

                    }

                    else if (Program.EXCH_CODE.Contains("KASE_SPOT"))
                    {
                        QuickFix.FIX44.NewOrderSingle ord1 = new QuickFix.FIX44.NewOrderSingle(
                        new ClOrdID(r.Id.ToString()),
                        new Symbol(r.Ticker),
                        new Side(s),
                        new TransactTime(DateTime.Now)
                        //ordType
                        );

                        ord1.Set(ordType);
                        ord1.Set(new Account(r.Acc));

                        s = ' ';
                        if (r.TimeInForce.ToUpper() == "DAY") s = TimeInForce.DAY;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE") s = TimeInForce.GOOD_TILL_DATE;
                        else if (r.TimeInForce.ToUpper() == "IMMEDIATE_OR_CANCEL") s = TimeInForce.IMMEDIATE_OR_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CANCEL") s = TimeInForce.GOOD_TILL_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CROSSING") s = TimeInForce.GOOD_TILL_CROSSING;
                        else if (r.TimeInForce.ToUpper() == "FILL_OR_KILL") s = TimeInForce.FILL_OR_KILL;
                        else
                        {
                            //здесь нужно ставить логирование
                            continue;
                        }
                        ord1.Set(new TimeInForce(s));

                        //if (r.MaxFloor is not null)
                        //{
                        //    if (r.MaxFloor > 0) //ord1.Set(new DisplayQty(r.MaxFloor.Value));
                        //        ord1.SetField(new DisplayQty(r.MaxFloor.Value));
                        //}

                        ord1.Set(new OrderQty((int)r.Quantity.Value));
                        
                        if (ordType.Value == OrdType.LIMIT || ordType.Value == OrdType.STOP_LIMIT)
                            ord1.Set(new Price(r.Price.Value));

                        //ord1.SetField(new NoTradingSessions(1));


                        //ord1.Set(new HandlInst('1'));
                        

                        if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE")
                            ord1.Set(new ExpireDate(r.ExpirationDate.Value.ToString("yyyyMMdd")));



                        //ord1.SetField(new NoTradingSessions(1));
                        //var sessionGroup1 = new QuickFix.FIX44.NewOrderSingle.NoTradingSessionsGroup();
                        //sessionGroup1.SetField(new TradingSessionID(r.Board));
                        //ord1.AddGroup(sessionGroup1);

                        ord1.SetField(new NoPartyIDs(3));
                        var partyIdGroup = new QuickFix.FIX44.NewOrderSingle.NoPartyIDsGroup();
                        partyIdGroup.SetField(new PartyID(r.SenderSubID));
                        partyIdGroup.SetField(new PartyIDSource(char.Parse(r.PartyIDSource)));
                        partyIdGroup.SetField(new PartyRole(int.Parse(r.PartyRole)));
                        ord1.AddGroup(partyIdGroup);


                        ord1.Header.GetString(Tags.BeginString);

                        SendMessage(ord1);
                        r.Processed_Status = "send";

                    }
                
                    else if (Program.EXCH_CODE.Contains("KASE"))
                    {
                        
                        QuickFix.FIX44.NewOrderSingle ord1 = new QuickFix.FIX44.NewOrderSingle(
                            new ClOrdID(r.Id.ToString()),
                            new Symbol(r.Ticker),
                            new Side(s),
                            new TransactTime(DateTime.Now)
                            //ordType
                            );
                        
                        //ord1.SetField(new NoTradingSessions(1));


                        ord1.Set(ordType);
                        //ord1.Set(new HandlInst('1'));
                        /*
                        if(!ok)
                        {
                            Console.WriteLine("Ошибка преобразования в int. Quatity = " + r.Quantity.Value.ToString());
                            return;
                        }
                        */
                        ord1.Set(new OrderQty((int)r.Quantity.Value));
                        //decimal qty = decimal.Parse(r.Quantity.Value.ToString());
                        //ord1.Set(new OrderQty((int)qty));
                        //if(!string.IsNullOrEmpty(r.Board)) ord1.Set(new TradingSessionID(r.Board));

                        s = ' ';
                        if (r.TimeInForce.ToUpper() == "DAY") s = TimeInForce.DAY;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE") s = TimeInForce.GOOD_TILL_DATE;
                        else if (r.TimeInForce.ToUpper() == "IMMEDIATE_OR_CANCEL") s = TimeInForce.IMMEDIATE_OR_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CANCEL") s = TimeInForce.GOOD_TILL_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CROSSING") s = TimeInForce.GOOD_TILL_CROSSING;
                        else if (r.TimeInForce.ToUpper() == "FILL_OR_KILL") s = TimeInForce.FILL_OR_KILL;
                        else
                        {
                            //здесь нужно ставить логирование
                            continue;
                        }
                        ord1.Set(new TimeInForce(s));
                        if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE")
                            ord1.Set(new ExpireDate(r.ExpirationDate.Value.ToString("yyyyMMdd")));

                        if (ordType.Value == OrdType.LIMIT || ordType.Value == OrdType.STOP_LIMIT)
                            ord1.Set(new Price(r.Price.Value));

                        if (r.MaxFloor is not null)
                        {
                            if (r.MaxFloor > 0) ord1.Set(new MaxFloor(r.MaxFloor.Value));
                        }

                        ord1.Set(new Account(r.Acc));

                        ord1.SetField(new NoTradingSessions(1));
                        var sessionGroup1 = new QuickFix.FIX44.NewOrderSingle.NoTradingSessionsGroup();
                        sessionGroup1.SetField(new TradingSessionID(r.Board));
                        ord1.AddGroup(sessionGroup1);

                        ord1.Header.GetString(Tags.BeginString);

                        SendMessage(ord1);
                        r.Processed_Status = "send";

                    }
                    else if (Program.EXCH_CODE == "ITS")
                    {
                        QuickFix.FIX50SP2.NewOrderSingle ord1 = new QuickFix.FIX50SP2.NewOrderSingle();
                        ord1.SetField(new ClOrdID(r.Id.ToString()));
                        ord1.SetField(new TransactTime(DateTime.Now));
                        if (string.IsNullOrEmpty(r.Ticker))
                        {
                            Console.WriteLine("Не указан тикер в таблице newOrders. Id записи = " + r.Id);
                            continue;
                        }
                        ord1.SetField(new ExDestination(r.exDestination));      //пул ликвидности                  
                        ord1.SetField(new SecurityID(r.Ticker));

                        ord1.SetField(new Side(s));
                        ord1.SetField(ordType);
                        s = ' ';
                        if (r.TimeInForce.ToUpper() == "DAY") s = TimeInForce.DAY;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_DATE") s = TimeInForce.GOOD_TILL_DATE;
                        else if (r.TimeInForce.ToUpper() == "IMMEDIATE_OR_CANCEL") s = TimeInForce.IMMEDIATE_OR_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CANCEL") s = TimeInForce.GOOD_TILL_CANCEL;
                        else if (r.TimeInForce.ToUpper() == "GOOD_TILL_CROSSING") s = TimeInForce.GOOD_TILL_CROSSING;
                        else if (r.TimeInForce.ToUpper() == "FILL_OR_KILL") s = TimeInForce.FILL_OR_KILL;
                        else
                        {
                            Console.WriteLine("Неверно указано поле TimeInForce . Id записи = " + r.Id);
                            //здесь нужно ставить логирование
                            continue;
                        }
                        ord1.Set(new TimeInForce(s));
                        if (ordType.Value == OrdType.LIMIT || ordType.Value == OrdType.STOP_LIMIT)
                        {
                            if(r.Price==null)
                            {
                                Console.WriteLine("Не задано значение Price . Id записи = " + r.Id);
                                //здесь нужно ставить логирование
                                continue;
                            }
                            ord1.Set(new Price(r.Price.Value));
                        }
                        if (r.price1 != null)
                        {
                            var price1 = new DecimalField(10104, r.price1.Value);
                            ord1.SetField(price1);
                        }

                        if (r.Quantity == null)
                        {
                            Console.WriteLine("Не задано значение Quantity. Id записи = " + r.Id);
                            //здесь нужно ставить логирование
                            continue;
                        }
                        ord1.SetField(new OrderQty((int)r.Quantity));

                        if(r.MaxFloor!=null && r.MaxFloor>0)
                        {
                            ord1.SetField(new DisplayQty((int)r.MaxFloor.Value));
                            ord1.SetField(new DisplayMethod('1'));
                        }
                        
                        ord1.SetField(new Account(r.Acc));


                        //Для подачи заявок с клиентского ТКСА поля будут заполнены следующим образом
                        //1, Account(ТКС): STDICLIENT
                        //448, PartyId: 387
                        //447, PartyIdSource: D
                        //452, PartyRole: 1
                        //448, PartyId: любой из перечисленных клиентских кодов: STDIc10 \ STDIc12 \ STDIc13
                        //447, PartyIdSource: D
                        //452, PartyRole: 3

                        //Для подачи заявок с собственного ТКСА поля будут заполнены следующим образом
                        //1, Account(ТКС): STDIOWN
                        //448, PartyId: 387
                        //447, PartyIdSource: D
                        //452, PartyRole: 1
                        //448, PartyId: stimk
                        //447, PartyIdSource: D
                        //452, PartyRole: 3
                        
                        ord1.SetField(new NoPartyIDs(3));
                        //первый элемент ставим хардкодом в зависимости от номера счета
                        //второй элемент берем из таблицы                        

                        var partyIdGroup = new QuickFix.FIX50SP2.NewOrderSingle.NoPartyIDsGroup();
                        if (r.Acc == "STDICLIENT")
                        {
                            partyIdGroup.SetField(new PartyID("387"));
                            partyIdGroup.SetField(new PartyIDSource(char.Parse("D")));
                            partyIdGroup.SetField(new PartyRole(int.Parse("1")));
                            ord1.AddGroup(partyIdGroup);
                        }
                        else if (r.Acc == "STDIOWN")
                        {
                            partyIdGroup.SetField(new PartyID("387"));
                            partyIdGroup.SetField(new PartyIDSource(char.Parse("D")));
                            partyIdGroup.SetField(new PartyRole(int.Parse("1")));
                            ord1.AddGroup(partyIdGroup);
                        }
                        partyIdGroup.SetField(new PartyID(r.SenderSubID));
                        partyIdGroup.SetField(new PartyIDSource(char.Parse(r.PartyIDSource)));
                        partyIdGroup.SetField(new PartyRole(int.Parse(r.PartyRole)));
                        ord1.AddGroup(partyIdGroup);

                        
                        ord1.Header.GetString(Tags.BeginString);

          
                        SendMessage(ord1);
                        r.Processed_Status = "send";
                        //lstIdSended.Add(r.Id);

                    }

                }
                //rz.Where(r=>lstIdSended.Contains(r.Id)).ToList().ForEach(r => { r.Processed_Status = "processed"; r.Processed_Time = DateTime.Now; });
                rz.Where(r => r.Processed_Status =="send").ToList().ForEach(r => { r.Processed_Status = "processed"; r.Processed_Time = DateTime.Now; });

                db.SaveChanges();

                if(DateTime.Now.AddSeconds(1) < startTime)
                {
                    Thread.Sleep(1000);
                }
                
            }
            catch(Exception err) 
            {
                //здесь надо поставить логирование
                Console.WriteLine(err.ToString());
            }

        }
        

        public static string GenerateNewPassword(int length = 8)
        {
            if (length < 3) // минимум 3 символа, чтобы вместить все категории
                throw new ArgumentException("Длина пароля должна быть не меньше 3.", nameof(length));

            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string allChars = upper + lower + digits;

            char[] result = new char[length];
            RandomNumberGenerator rng = RandomNumberGenerator.Create();

            // Функция для выбора случайного символа из строки
            char GetRandomChar(string chars)
            {
                byte[] buffer = new byte[1];
                rng.GetBytes(buffer);
                return chars[buffer[0] % chars.Length];
            }

            // Обязательные символы
            result[0] = GetRandomChar(upper);
            result[1] = GetRandomChar(lower);
            result[2] = GetRandomChar(digits);

            // Остальные символы
            for (int i = 3; i < length; i++)
            {
                result[i] = GetRandomChar(allChars);
            }

            // Перемешиваем массив (Fisher-Yates shuffle)
            for (int i = result.Length - 1; i > 0; i--)
            {
                byte[] buffer = new byte[1];
                rng.GetBytes(buffer);
                int j = buffer[0] % (i + 1);

                (result[i], result[j]) = (result[j], result[i]);
            }

            return new string(result);
        }
      

        #region Message creation functions
        private QuickFix.FIX44.NewOrderSingle QueryNewOrderSingle44()
        {
            OrdType ordType = QueryOrdType();

            QuickFix.FIX44.NewOrderSingle newOrderSingle = new QuickFix.FIX44.NewOrderSingle(
                QueryClOrdId(),
                QuerySymbol(),
                QuerySide(),
                new TransactTime(DateTime.Now)
                //ordType
                );

            newOrderSingle.Set(new HandlInst('1'));
            newOrderSingle.Set(QueryOrderQty());
            newOrderSingle.Set(QueryTimeInForce());
            if (ordType.Value == OrdType.LIMIT || ordType.Value == OrdType.STOP_LIMIT)
                newOrderSingle.Set(QueryPrice());
            if (ordType.Value == OrdType.STOP || ordType.Value == OrdType.STOP_LIMIT)
                newOrderSingle.Set(QueryStopPx());

            return newOrderSingle;
        }

        private QuickFix.FIX44.OrderCancelRequest QueryOrderCancelRequest44()
        {
            QuickFix.FIX44.OrderCancelRequest orderCancelRequest = new QuickFix.FIX44.OrderCancelRequest(
                QueryOrigClOrdId(),
                QueryClOrdId(),
                QuerySymbol(),
                QuerySide(),
                new TransactTime(DateTime.Now));

            orderCancelRequest.Set(QueryOrderQty());
            return orderCancelRequest;
        }

        private QuickFix.FIX44.OrderCancelReplaceRequest QueryCancelReplaceRequest44()
        {
            QuickFix.FIX44.OrderCancelReplaceRequest ocrr = new QuickFix.FIX44.OrderCancelReplaceRequest(
                QueryOrigClOrdId(),
                QueryClOrdId(),
                QuerySymbol(),
                QuerySide(),
                new TransactTime(DateTime.Now),
                QueryOrdType());

            ocrr.Set(new HandlInst('1'));
            if (QueryConfirm("New price"))
                ocrr.Set(QueryPrice());
            if (QueryConfirm("New quantity"))
                ocrr.Set(QueryOrderQty());

            return ocrr;
        }

        private QuickFix.FIX44.MarketDataRequest QueryMarketDataRequest44()
        {
            MDReqID mdReqID = new MDReqID("MARKETDATAID");
            SubscriptionRequestType subType = new SubscriptionRequestType(SubscriptionRequestType.SNAPSHOT);
            MarketDepth marketDepth = new MarketDepth(0);

            QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup marketDataEntryGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
            marketDataEntryGroup.Set(new MDEntryType(MDEntryType.BID));

            QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup symbolGroup = new QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup();
            symbolGroup.Set(new Symbol("LNUX"));

            QuickFix.FIX44.MarketDataRequest message = new QuickFix.FIX44.MarketDataRequest(mdReqID, subType, marketDepth);
            message.AddGroup(marketDataEntryGroup);
            message.AddGroup(symbolGroup);

            return message;
        }
        #endregion

        #region field query private methods
        private ClOrdID QueryClOrdId()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.Write("ClOrdID? ");
            return new ClOrdID(ReadCommand());
        }

        private OrigClOrdID QueryOrigClOrdId()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.Write("OrigClOrdID? ");
            return new OrigClOrdID(ReadCommand());
        }

        private Symbol QuerySymbol()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.Write("Symbol? ");
            return new Symbol(ReadCommand());
        }

        private Side QuerySide()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.WriteLine("1) Buy");
            if(isDebug) Console.WriteLine("2) Sell");
            if(isDebug) Console.WriteLine("3) Sell Short");
            if(isDebug) Console.WriteLine("4) Sell Short Exempt");
            if(isDebug) Console.WriteLine("5) Cross");
            if(isDebug) Console.WriteLine("6) Cross Short");
            if(isDebug) Console.WriteLine("7) Cross Short Exempt");
            if(isDebug) Console.Write("Side? ");
            string s = ReadCommand();

            char c = ' ';
            switch (s)
            {
                case "1": c = Side.BUY; break;
                case "2": c = Side.SELL; break;
                case "3": c = Side.SELL_SHORT; break;
                case "4": c = Side.SELL_SHORT_EXEMPT; break;
                case "5": c = Side.CROSS; break;
                case "6": c = Side.CROSS_SHORT; break;
                case "7": c = 'A'; break;
                default: throw new Exception("unsupported input");
            }
            return new Side(c);
        }

        private OrdType QueryOrdType()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.WriteLine("1) Market");
            if(isDebug) Console.WriteLine("2) Limit");
            if(isDebug) Console.WriteLine("3) Stop");
            if(isDebug) Console.WriteLine("4) Stop Limit");
            if(isDebug) Console.Write("OrdType? ");
            string s = ReadCommand();

            char c = ' ';
            switch (s)
            {
                case "1": c = OrdType.MARKET; break;
                case "2": c = OrdType.LIMIT; break;
                case "3": c = OrdType.STOP; break;
                case "4": c = OrdType.STOP_LIMIT; break;
                default: throw new Exception("unsupported input");
            }
            return new OrdType(c);
        }

        private OrderQty QueryOrderQty()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.Write("OrderQty? ");
            //return new OrderQty(Convert.ToDecimal(ReadCommand()));
            return new OrderQty(Convert.ToInt32(ReadCommand()));
        }

        private TimeInForce QueryTimeInForce()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.WriteLine("1) Day");
            if(isDebug) Console.WriteLine("2) IOC");
            if(isDebug) Console.WriteLine("3) OPG");
            if(isDebug) Console.WriteLine("4) GTC");
            if(isDebug) Console.WriteLine("5) GTX");
            if(isDebug) Console.Write("TimeInForce? ");
            string s = ReadCommand();

            char c = ' ';
            switch (s)
            {
                case "1": c = TimeInForce.DAY; break;
                case "2": c = TimeInForce.IMMEDIATE_OR_CANCEL; break;
                case "3": c = TimeInForce.AT_THE_OPENING; break;
                case "4": c = TimeInForce.GOOD_TILL_CANCEL; break;
                case "5": c = TimeInForce.GOOD_TILL_CROSSING; break;
                default: throw new Exception("unsupported input");
            }
            return new TimeInForce(c);
        }

        private Price QueryPrice()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.Write("Price? ");
            return new Price(Convert.ToDecimal(ReadCommand()));
        }

        private StopPx QueryStopPx()
        {
            if(isDebug) Console.WriteLine();
            if(isDebug) Console.Write("StopPx? ");
            return new StopPx(Convert.ToDecimal(ReadCommand()));
        }

        #endregion
    }
}
