using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeClient
{
    public class instrsView
    {
        [NotMapped]
        public string requestId { get; set; }
        public string symbol { get; set; }
        public string codeMubasher { get; set; }       
    }
    public class NewOrders
    {
        public int Id { get; set; }
        public DateTime? InpDate { get; set; }
        public byte? IsReal { get; set; }
        public string? Processed_Status { get; set; }
        public DateTime? Processed_Time { get; set; }
        
        public string? ClientId { get; set; }
        public string? OrderID_AIS { get; set; }
        public string? OrigClOrderID { get; set; }
        public string? ExchangeCode { get; set; }
        public string? Ticker { get; set; }
        public string? Board { get; set; }
        
        public string? Bloom_ExchCode { get; set; }
        public string? Direction { get; set; }
        public decimal? Price { get; set; }
        public decimal? Quantity { get; set; }
        public string? Type { get; set; }
        public string? Serial { get; set; }
        public string? Comments { get; set; }
        public string? Isin { get; set; }
        
        public string? Acc { get; set; }
        public string? Investor { get; set; }
        public byte? IsMMOrder { get; set; }
        public string? SenderSubID { get; set; }
        public string? TimeInForce { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? UnderlyingInstrument { get; set; }
        public string? ClientName { get; set; }
        public string? OrderNumberAIS { get; set; }
        public DateTime? OrderDateAIS { get; set; }
        public DateTime? RegisteredDateAIS { get; set; }
        public string? RejectingOrderID { get; set; }
        public string? Currency { get; set; }
        public string? Handlinst { get; set; }
        public string? ContrBroker { get; set; }
        public string? PartyIDSource { get; set; }
        public string? PartyRole { get; set; }
        public decimal? MaxFloor { get; set; }
        public string? exDestination { get; set; }
        public decimal? price1 { get; set; }
        public int? msgNum { get; set; }
    }
    public class tradeCapture
    {
        public int ID { get; set; }
        public DateTime? INPDATE { get; set; }
        public byte? isReal { get; set; }
        public string? ClOrdID { get; set; }
        public byte? sendToAIS { get; set; }
        public DateTime? sendToAIStime { get; set; }
        public string? clientID { get; set; }
        public string? orderID_AIS { get; set; }
        public string? Account { get; set; }
        public string? exchangeCode { get; set; }
        public string? Side { get; set; }
        public string? ticker { get; set; }
        public string? board { get; set; }
        public string? UnderlyingSymbol { get; set; }
        public string? TradeReportID { get; set; }
        public string? TrdType { get; set; }
        public string? TrdSubType { get; set; }
        public string? SecondaryTradeReportID { get; set; }
        public string? ExecType { get; set; }
        public string? ExecID { get; set; }
        public string? LastQty { get; set; }
        public string? LastPx { get; set; }
        public string? CalculatedCcyLastQty { get; set; }
        public string? TradeDate { get; set; }
        public string? TransactTime { get; set; }
        public string? SettlDate { get; set; }
        public string? OptionSettlType { get; set; }
        public string? OrderID { get; set; }
        public string? SecondaryClOrdID { get; set; }
        public string? TradingSessionSubID { get; set; }
        public string? GrossTradeAmt { get; set; }
        public string? AccruedInterestAmt { get; set; }
        public string? SettlCurrAmt { get; set; }
        public string? EndAccruedInterestAmt { get; set; }
        public string? StartCash { get; set; }
        public string? EndCash { get; set; }
        public string? MiscFeeAmt_Exchange { get; set; }
        public string? MiscFeeAmt_Clearing { get; set; }
        public string? MiscFeeAmt_Access { get; set; }
        public string? SettlInstID { get; set; }
        public string? Price2 { get; set; }
        public string? Price { get; set; }
        public string? PriceType { get; set; }
        public string? InstitutionID { get; set; }
        public string? CurrencyCode { get; set; }
        public string? ClientAccID { get; set; }
        public string? ParentID { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? Yield { get; set; }
        public string? Commission { get; set; }
        public string? CommType { get; set; }
    }


}
