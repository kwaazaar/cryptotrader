using System;
using System.Collections.Generic;
using System.Text;

namespace Poloniex.APIClient.Models
{
    public class BalanceDetails
    {
        public decimal Available { get; set; }
        public decimal OnOrders { get; set; }
        public decimal BtcValue { get; set; }
    }
}
