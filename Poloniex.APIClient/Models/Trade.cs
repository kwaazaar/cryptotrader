using System;
using System.Collections.Generic;
using System.Text;

namespace Poloniex.APIClient.Models
{
    public class Trade
    {
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public decimal Rate { get; set; }
        public decimal Total { get; set; }
        public string TradeId { get; set; }
        public string Type { get; set; }
    }
}
