using System;
using System.Collections.Generic;
using System.Text;

namespace Poloniex.APIClient.Models
{
    public class Order
    {
        public int Id { get; set; }

        public string Currency { get; set; }
        public string BaseCurrency { get; set; }
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public decimal Fee { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime Timestamp { get; set; }

    }
}
