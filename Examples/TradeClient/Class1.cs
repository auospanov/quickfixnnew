using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeClient
{
    public enum TradingSessionID1
    {
        CloseCrossing = 9,
        ContinuousMatching = 6,
        EndOfIPO = 17,
        EndOfTrading = 10,
        IPOBookbuilding = 16,
        IPOMarketClose = 20,
        IPOStatistics = 19,
        MarketClose = 15,
        OpenCrossing = 7,
        PreClose = 8,
        PreOpen = 5,
        TemporaryHalting = 14,
        TradeStatistics = 13,
        TradingAtLast = 11
    }
    public static class TradingSessionNames
    {
        public const string CloseCrossing = "CLOSE_CROSSING";
        public const string ContinuousMatching = "CONT_MATCHING";
        public const string EndOfIPO = "END_OF_IPO";
        public const string EndOfTrading = "END_OF_TRADING";
        public const string IPOBookbuilding = "IPO_BOOKBUILDING";
        public const string IPOMarketClose = "IPO_MARKET_CLOSE";
        public const string IPOStatistics = "IPO_STATISTICS";
        public const string MarketClose = "MARKET_CLOSE";
        public const string OpenCrossing = "OPEN_CROSSING";
        public const string PreClose = "PRE_CLOSE";
        public const string PreOpen = "PRE_OPEN";
        public const string TemporaryHalting = "TEMPORARY_HALTING";
        public const string TradeStatistics = "TRADE_STATISTICS";
        public const string TradingAtLast = "TRADING_AT_LAST";
    }
   
}
