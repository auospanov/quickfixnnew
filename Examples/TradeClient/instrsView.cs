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
        public string Processed_Status { get; set; }
        public DateTime? Processed_Time { get; set; }
        public string ClientId { get; set; }
        public string OrderID_AIS { get; set; }
        public string OrigClOrderID { get; set; }
        public string ExchangeCode { get; set; }
        public string Ticker { get; set; }
        public string Board { get; set; }
        public string Bloom_ExchCode { get; set; }
        public string Direction { get; set; }
        public decimal? Price { get; set; }
        public decimal? Quantity { get; set; }
        public string Type { get; set; }
        public string Serial { get; set; }
        public string Comments { get; set; }
        public string Isin { get; set; }
        public string Acc { get; set; }
        public string Investor { get; set; }
        public byte? IsMMOrder { get; set; }
        public string SenderSubID { get; set; }
        public string TimeInForce { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string UnderlyingInstrument { get; set; }
        public string ClientName { get; set; }
        public string OrderNumberAIS { get; set; }
        public DateTime? OrderDateAIS { get; set; }
        public DateTime? RegisteredDateAIS { get; set; }
        public string RejectingOrderID { get; set; }
        public string Currency { get; set; }
        public string Handlinst { get; set; }
        public string ContrBroker { get; set; }
        public string PartyIDSource { get; set; }
        public string PartyRole { get; set; }
    }


}
