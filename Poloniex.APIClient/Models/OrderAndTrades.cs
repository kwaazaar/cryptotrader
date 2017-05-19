using System;
using System.Collections.Generic;
using System.Text;

namespace Poloniex.APIClient.Models
{
    public class OrderAndTrades
    {
        // {"orderNumber":31226040,"resultingTrades":[{"amount":"338.8732","date":"2014-10-18 23:03:21","rate":"0.00000173","total":"0.00058625","tradeID":"16164","type":"buy"}]}
        public string OrderNumber { get; set; }
        public IEnumerable<Trade> ResultingTrades { get; set; }
    }
}
