using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Poloniex.APIClient.Models
{
    // https://poloniex.com/support/api/

    public class Ticker
    {
        [JsonProperty("currency")]
        public string Currency { get; set; }
        [JsonProperty("baseCurrency")]
        public string BaseCurrency { get; set; }

        [JsonProperty("last")]
        public decimal Last { get; set; }
        [JsonProperty("lowestAsk")]
        public decimal LowestAsk { get; set; }
        [JsonProperty("highestBid")]
        public decimal HighestBid { get; set; }
        [JsonProperty("percentChange")]
        public decimal PercentageChange { get; set; }
        [JsonProperty("baseVolume")]
        public decimal BaseVolume { get; set; }
        [JsonProperty("quoteVolume")]
        public decimal QuoteVolume { get; set; }
        [JsonProperty("isFrozen")]
        public bool IsFrozen { get; set; }
        [JsonProperty("24hrHigh")]
        public decimal TwentyFourHrHigh { get; set; }
        [JsonProperty("24hrLow")]
        public decimal TwentyFourHrLow { get; set; }

        public decimal Average { get; set; }
        public decimal? CurvePct { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            return $"{Currency}_{BaseCurrency}: {Timestamp:T} - {Last}";
        }
    }
}
