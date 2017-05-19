using System;
using System.Collections.Generic;
using System.Text;

namespace Poloniex.APIClient.Models
{
    public class OpenOrder
    {
        // [{"orderNumber":"120466","type":"sell","rate":"0.025","amount":"100","total":"2.5"},{"orderNumber":"120467","type":"sell","rate":"0.04","amount":"100","total":"4"}, ... ]
        public string OrderNumber { get; set; }
        public string Type { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public decimal Total { get; set; }
    }
}
