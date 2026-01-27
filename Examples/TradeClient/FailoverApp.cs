using QuickFix;
using QuickFix.Logger;
using QuickFix.Transport;
using System;
using QuickFix.Store;
using System.IO;

namespace TradeClient
{
   
    public class FailoverApp : MessageCracker, IApplication
    {
        private SocketInitiator initiator;
        private SessionSettings settings;
        private ILogFactory logFactory;
        private IMessageStoreFactory storeFactory;   // ← правильный интерфейс
        private IMessageFactory messageFactory;

        private string primaryConfig = "fix_aix.cfg";
        private string backupConfig = "fix_aix_rezerv.cfg";

        public void OnCreate(SessionID sessionID) { }
        public void OnLogon(SessionID sessionID)
        {
            Console.WriteLine($"Logon: {sessionID}");
        }
        public void OnLogout(SessionID sessionID)
        {
            Console.WriteLine($"Logout: {sessionID}");
            TryFailover(sessionID);
        }
        public void ToAdmin(Message message, SessionID sessionID) { }
        public void FromAdmin(Message message, SessionID sessionID) { }
        public void ToApp(Message message, SessionID sessionID) { }
        public void FromApp(Message message, SessionID sessionID) { }

        public void Start()
        {
            string cfg = File.ReadAllText(primaryConfig);
            TextReader textReader = new StringReader(cfg);
            settings = new SessionSettings(textReader);
            storeFactory = new FileStoreFactory(settings);   // ← реализация
            logFactory = new FileLogFactory(settings);
            messageFactory = new DefaultMessageFactory();

            initiator = new SocketInitiator(this, storeFactory, settings, logFactory, messageFactory);
            initiator.Start();
        }

        private void TryFailover(SessionID sessionID)
        {
            Console.WriteLine("Attempting failover...");
            initiator.Stop();

            string cfg = File.ReadAllText(backupConfig);
            TextReader textReader = new StringReader(cfg);
            settings = new SessionSettings(textReader);
            storeFactory = new FileStoreFactory(settings);
            logFactory = new FileLogFactory(settings);
            messageFactory = new DefaultMessageFactory();

            initiator = new SocketInitiator(this, storeFactory, settings, logFactory, messageFactory);
            initiator.Start();
        }
    }
}
