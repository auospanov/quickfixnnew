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
        public string ProcedureJson { get; set; } = "";
    }

    public sealed class GlobalGlassContainer
    {
        public List<GlassContainerDto> glassContainers { get; set; } = new();
        public string ObjectId { get; set; } = "";
    }

    public sealed class GlassContainerDto
    {
        [JsonProperty("objectType")]
        public string ObjectType { get; set; } = "GLASS";

        [JsonProperty("sourceName")]
        public string SourceName { get; set; } = "";

        [JsonProperty("ticker")]
        public string Ticker { get; set; } = "";

        [JsonProperty("shortName")]
        public string ShortName { get; set; } = "";

        [JsonProperty("board")]
        public string Board { get; set; } = "";

        [JsonProperty("data")]
        public string Data { get; set; } = "";
    }

    public sealed class GlassEntryDto
    {
        [JsonProperty("askQuantity")]
        public string AskQuantity { get; set; } = "-";

        [JsonProperty("priceStr")]
        public string PriceStr { get; set; } = "";

        [JsonProperty("bidQuantity")]
        public string BidQuantity { get; set; } = "-";

        [JsonProperty("best")]
        public string Best { get; set; } = "";

        [JsonProperty("fontColor")]
        public string FontColor { get; set; } = "#000000";

        [JsonProperty("colorBuy")]
        public string ColorBuy { get; set; } = "#E8F5E9";

        [JsonProperty("colorSell")]
        public string ColorSell { get; set; } = "#FFEBEE";

        [JsonProperty("price")]
        public decimal Price { get; set; }
    }
}
