using Microsoft.Extensions.Configuration;
using Poloniex.APIClient.Models;
using Poloniex.APIClient.Repositories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WampSharp.V2;
using WampSharp.V2.Client;

namespace Poloniex.APIClient
{
    class Program
    {
        static string connectionString = null;
        static decimal maxBuyPrice = 0m;
        static string poloKey = null;
        static string poloSecret = null;

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
            var config = builder.Build();

            connectionString = config["connectionString"];
            maxBuyPrice = decimal.Parse(config["maxBuyPrice"]);
            poloKey = config["polo:key"];
            poloSecret = config["polo:secret"];
            
            if (maxBuyPrice > 0.5m) throw new InvalidOperationException("maxBuyPrice too high");

            var client = new PoloniexAPIClient(poloKey, poloSecret);
            client.Connect().GetAwaiter().GetResult();
            var btcBalance = client.GetBalance("ETH");
            var openOrders = client.GetOpenOrders("BTC_STR");

            // Check a specific period
            /*
            for (int m = 29; m < 30; m++)
            {
                var periodEndUtc = new DateTime(2017, 5, 18, 14, m, 0, DateTimeKind.Utc);
                var periodStartUtc = periodEndUtc.Subtract(CurveConfig.Drop().PeriodLength);
                List<Ticker> testTickerData = null;
                using (var repo = new CryptoTradingRepo(new SqlConnection(connectionString), TimeSpan.FromSeconds(5)))
                    testTickerData = repo.GetTicker("STR", "BTC", periodStartUtc, periodEndUtc).GetAwaiter().GetResult().ToList();
                var analyzer = new DataAnalyzer(testTickerData);
                var isDrop = analyzer.CheckCurve(periodEndUtc, CurveConfig.Drop());
                isDrop.Messages.ForEach(msg => Console.WriteLine(msg));
                Console.WriteLine($"Is Drop-{m}: {isDrop.Result}, drop-pct: {isDrop.Value}");
            }
            */

            var cancellationTokenSource = new CancellationTokenSource();
            List<Ticker> tickerData = new List<Ticker>();
            client.Ticker("BTC_ETH", onTicker: (t) => ProcessTickerData(tickerData, t), onException: (ex) =>
                {
                    Console.WriteLine("API-call error: " + ex.ToString());
                },
                cancellationToken: cancellationTokenSource.Token);

            Console.WriteLine("Press Ctrl+C to stop");
            var handle = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) => { handle.Set(); e.Cancel = true; }; // Cancel must be true, to make sure the process is not killed and we can clean up nicely below
            handle.WaitOne();
            cancellationTokenSource.Cancel();
        }

        public static void ProcessTickerData(List<Ticker> tickerDataIn, Ticker t)
        {
            tickerDataIn.Add(t);

            var periodLength = CurveConfig.Drop().PeriodLength;
            var tickerData = tickerDataIn
                .Where(td => td.Timestamp >= DateTime.UtcNow.Subtract(periodLength))
                .OrderBy(td => td.Timestamp)
                .ToList();

            tickerDataIn = tickerData; // Ook limiteren (optioneel) ivm memory usage

            var analyzer = new DataAnalyzer(tickerData);
            var dropRes = analyzer.CheckCurve(t.Timestamp, CurveConfig.Drop());
            if (dropRes.Result)
                t.CurvePct = dropRes.Value;
            else
            {
                var raiseRes = analyzer.CheckCurve(t.Timestamp, CurveConfig.Raise());
                if (raiseRes.Result)
                    t.CurvePct = raiseRes.Value;
            }

            Console.Write(t.ToString() + "...");
            if (t.CurvePct.HasValue)
                Console.Write($"({t.CurvePct.Value * 100:N2} % curve!)...");

            try
            {
                using (var repo = new CryptoTradingRepo(new SqlConnection(connectionString), TimeSpan.FromSeconds(5)))
                {
                    repo.StoreTicker(t).GetAwaiter().GetResult();
                }
                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Repo-error: " + ex.ToString());
            }

        }
    }
}
