using Newtonsoft.Json;
using System.Collections.Generic;

namespace TradeClient
{
    public sealed class FixUpdateListItem
    {
        public string idObject { get; set; } = "";
        public decimal? bid { get; set; }
        public decimal? ask { get; set; }
        public decimal? last { get; set; }
        public decimal? changepct1d { get; set; }
    }

    public sealed class SignalRQuoteUpdateDto
    {
        public string IdObject { get; set; } = "";
        public string MessageText { get; set; } = "";
    }

    public sealed class GlobalGlassContainer
    {
        public List<GlassContainerDto> glassContainers { get; set; } = new();
        public string ObjectId { get; set; } = "";
    }

    public sealed class GlassContainerDto
    {
        [JsonProperty("ticker", Order = 1)]
        public string Ticker { get; set; } = "";

        [JsonProperty("data", Order = 2)]
        public List<GlassEntryDto> Data { get; set; } = new();

        [JsonProperty("sourceName", Order = 3)]
        public string SourceName { get; set; } = "";

        [JsonProperty("shortName", Order = 4)]
        public string ShortName { get; set; } = "";

        [JsonProperty("objectType", Order = 5)]
        public string ObjectType { get; set; } = "GLASS";
    }

    public sealed class GlassEntryDto
    {
        [JsonProperty("priceStr", Order = 1)]
        public string PriceStr { get; set; } = "";

        [JsonProperty("bidQuantity", Order = 2)]
        public object BidQuantity { get; set; } = "";

        [JsonProperty("price", Order = 3)]
        public decimal Price { get; set; }

        [JsonProperty("askQuantity", Order = 4)]
        public object AskQuantity { get; set; } = "";

        [JsonProperty("colorBuy", Order = 5)]
        public string ColorBuy { get; set; } = "";

        [JsonProperty("colorSell", Order = 6)]
        public string ColorSell { get; set; } = "";

        [JsonProperty("best", Order = 7)]
        public int Best { get; set; }

        [JsonProperty("fontColor", Order = 8)]
        public string FontColor { get; set; } = "#000000";
    }
}
