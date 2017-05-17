using System;
using System.Collections.Generic;
using System.Text;

namespace Poloniex.APIClient
{
    public class AnalysisResult
    {
        public List<string> Messages { get; private set; } = new List<string>();

        public bool Result { get; set; }
        public decimal Value { get; set; }

        public static AnalysisResult Create(bool result, decimal value, IEnumerable<string> messages = null)
        {
            var res = new AnalysisResult
            {
                Result = result,
                Value = value,
            };

            if (messages != null)
                res.Messages.AddRange(messages);

            return res;
        }
    }
}
