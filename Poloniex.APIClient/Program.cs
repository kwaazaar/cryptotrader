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
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
            var config = builder.Build();

            var connectionString = config["connectionString"];
            var maxBuyPrice = decimal.Parse(config["maxBuyPrice"]);
            if (maxBuyPrice > 0.5m) throw new InvalidOperationException("maxBuyPrice too high");

            var client = new PoloniexAPIClient();
            client.Connect().GetAwaiter().GetResult();

            var cancellationTokenSource = new CancellationTokenSource();

            List<Ticker> tickerData = new List<Ticker>();

            client.Ticker("BTC_STR",
                onTicker: (t) =>
                {
                    tickerData.Add(t);

                    // Na 1200 terugschroeven naar 1000
                    if (tickerData.Count > 1200)
                        tickerData = tickerData.OrderByDescending(td => td.Timestamp).Take(1000).OrderBy(td => td.Timestamp).ToList();

                    var analyzer = new DataAnalyzer(tickerData);
                    var dropRes = analyzer.CheckCurve(t.Timestamp, CurveConfig.Drop(minCurvePct: 0.08m));
                    if (dropRes.Result)
                        t.CurvePct = dropRes.Value;
                    else
                    {
                        var raiseRes = analyzer.CheckCurve(t.Timestamp, CurveConfig.Raise(minCurvePct: -0.08m));
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

                },
                onException: (ex) =>
                {
                    Console.WriteLine("API-call error: " + ex.ToString());
                },
                cancellationToken: cancellationTokenSource.Token);

            /*
            var analyzer = new DataAnalyzer(tickerData);
            var isDrop = analyzer.CheckCurve(utcNow, CurveConfig.Drop());

            isDrop.Messages.ForEach(m => Console.WriteLine(m));
            Console.WriteLine($"Is Drop: {isDrop.Result}, drop-pct: {isDrop.Value}");
            */

            Console.WriteLine("Press Ctrl+C to stop");
            var handle = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) => { handle.Set(); e.Cancel = true; }; // Cancel must be true, to make sure the process is not killed and we can clean up nicely below
            handle.WaitOne();
            cancellationTokenSource.Cancel();
        }
    }
}
