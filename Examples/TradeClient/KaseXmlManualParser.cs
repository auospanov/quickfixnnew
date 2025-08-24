using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace TradeClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;

    public class KaseItem
    {
        public decimal bid { get; set; }
        public string currency { get; set; }
        public string date { get; set; }
        public int dealNumber { get; set; }
        public int issuersid { get; set; }
        public int numOfShares { get; set; }
        public decimal offer { get; set; }
        public int orderNumber { get; set; }
        public decimal priceHigh { get; set; }
        public decimal priceLast { get; set; }
        public decimal priceLow { get; set; }
        public decimal priceOpen { get; set; }
        public decimal priceWA { get; set; }
        public string ticker { get; set; }
        public decimal volumeKZT { get; set; }
        public decimal volumeUSD { get; set; }
    }

    public static class KaseXmlManualParser
    {
        public static List<KaseItem> Parse(string xml)
        {
            var doc = XDocument.Parse(xml);
            var items = doc.Descendants().Where(x => x.Name.LocalName == "item");

            var result = new List<KaseItem>();

            foreach (var item in items)
            {
                result.Add(new KaseItem
                {
                    bid = ParseDecimal(item, "bid"),
                    currency = GetValue(item, "currency"),
                    date = GetValue(item, "date"),
                    dealNumber = ParseInt(item, "dealNumber"),
                    issuersid = ParseInt(item, "issuersid"),
                    numOfShares = ParseInt(item, "numOfShares"),
                    offer = ParseDecimal(item, "offer"),
                    orderNumber = ParseInt(item, "orderNumber"),
                    priceHigh = ParseDecimal(item, "priceHigh"),
                    priceLast = ParseDecimal(item, "priceLast"),
                    priceLow = ParseDecimal(item, "priceLow"),
                    priceOpen = ParseDecimal(item, "priceOpen"),
                    priceWA = ParseDecimal(item, "priceWA"),
                    ticker = GetValue(item, "ticker"),
                    volumeKZT = ParseDecimal(item, "volumeKZT"),
                    volumeUSD = ParseDecimal(item, "volumeUSD")
                });
            }

            return result;
        }

        private static string GetValue(XElement parent, string name)
            => parent.Element(parent.Name.Namespace + name)?.Value ?? "";

        private static int ParseInt(XElement parent, string name)
            => int.TryParse(GetValue(parent, name), out var v) ? v : 0;

        private static decimal ParseDecimal(XElement parent, string name)
            => decimal.TryParse(GetValue(parent, name), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

}
