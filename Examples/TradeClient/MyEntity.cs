using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace TradeClient
{
	public class heartbeat
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int ID {get;set;}
		//public DateTime INPDATE {get;set;}
		public byte isReal {get;set;}
		public string exchangeCode {get;set;}
		public byte isMM { get;set;}
		public DateTime lastTime {get;set;}
	}
    public class quotesSimple
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }
        public byte isReal { get; set; }
        public string exchangeCode { get; set; }
        public string ticker { get; set; }
        [Precision(18, 6)]
        public decimal? bid { get; set; }
        [Precision(18, 6)]
        public decimal? bidQuantity { get; set; }
        [Precision(18, 6)]
        public decimal? ask { get; set; }
        [Precision(18, 6)]
        public decimal? askQuantity { get; set; }
        [Precision(18, 6)]
        public decimal? lastTrade { get; set; }
        public string msgNum { get; set; }
        public string sendingTime { get; set; }

       
    }
    public class orders
	{
			 [Key]
			[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int ID {get;set;}
		//public DateTime INPDATE {get;set;}
		public byte isReal {get;set;}
		public string clientOrderID {get;set;}
		public string origClOrderID {get;set;}
		public string orderReferenceExchange {get;set;}
		public string serial {get;set;}
		//public byte sendToAIS {get;set;}
		//public DateTime sendToAIStime {get;set;}
		public string clientID {get;set;}
		public string orderID_AIS {get;set;}
		public string exchangeCode {get;set;}
		public string ticker {get;set;}
		public string board {get;set;}
		public string bloom_exchCode {get;set;}
		public string status {get;set;}
		public string direction {get;set;}
		public string type {get;set;}
		[Precision(18, 6)]
		public decimal leavesQty {get;set;}
		[Precision(18, 6)]
		public decimal price {get;set;}
		[Precision(18, 6)]
		public decimal quantity {get;set;}
		[Precision(18, 6)]
		public decimal priceDeal {get;set;}
		[Precision(18, 6)]
		public decimal quantityDeal {get;set;}
		[Precision(18, 6)]
		public decimal priceAvg {get;set;}
		[Precision(18, 6)]
		public decimal quantityDealTotal {get;set;}
		[Precision(18, 6)]
		public decimal? volume_Cash {get;set;}
		public string currency {get;set;}
		[Precision(18, 6)]
		public decimal? commissionContragent {get;set;}
		public string comments {get;set;}
		public string acc {get;set;}
		public string investor {get;set;}
		public byte isMMorder {get;set;}
		public string clientName {get;set;}
		public string UserName {get;set;}
		public string timeInForce {get;set;}
		public DateTime? expirationDate {get;set;}
		public DateTime executionTime {get;set;}
		public DateTime? settlementDate {get;set;}
		public string sessionId {get;set;}
		public string underlyingInstr {get;set;}
		public string underlyingInstrQty {get;set;}
		public DateTime? closeDate {get;set;}
		[Precision(18, 6)]
		public decimal? yield {get;set;}
		[Precision(18, 6)]
		public decimal? repoTax {get;set;}
		public string riskLevel {get;set;}
		[Precision(18, 6)]
		public decimal? closePrice {get;set;}
		public string whoRemoved {get;set;}
		public DateTime? whenRemoved {get;set;}
		public string TrdMatchID {get;set;}
		public long? msgNum { get;set;}
		public string fullMessage { get;set;}
        [Precision(18, 6)]
        public decimal? maxFloor { get; set; }
    }
}
