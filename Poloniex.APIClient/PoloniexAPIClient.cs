using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Poloniex.APIClient.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WampSharp.V2;
using WampSharp.V2.Client;

namespace Poloniex.APIClient
{
    public class PoloniexAPIClient : IDisposable
    {
        private readonly string _pushApiAddress;
        private readonly string _orderApiAddress;
        private readonly string _poloKey;
        private readonly byte[] _poloSecret;
        private static int nonce = 0;
        private static object nonceLock = new object();

        public PoloniexAPIClient(string poloKey, string poloSecret)
            : this("wss://api.poloniex.com", "https://poloniex.com/tradingApi", poloKey, poloSecret)
        {
        }

        public PoloniexAPIClient(string pushApiAddress, string orderApiAddress, string poloKey, string poloSecret)
        {
            _pushApiAddress = pushApiAddress;
            _orderApiAddress = orderApiAddress;
            _poloKey = poloKey;
            _poloSecret = Encoding.UTF8.GetBytes(poloSecret);
        }

        public async Task Connect()
        {
            DefaultWampChannelFactory channelFactory = new DefaultWampChannelFactory();
            var channel = channelFactory.CreateJsonChannel(_pushApiAddress, "realm1");
            await channel.Open().ConfigureAwait(false);
        }
        public void Ticker(Action<Ticker> onTicker, Action<Exception> onException, CancellationToken cancellationToken)
        {
            Ticker(null, onTicker, onException, cancellationToken);
        }

        public void Ticker(string currencyPairFilter, Action<Ticker> onTicker, Action<Exception> onException, CancellationToken cancellationToken)
        {
            Exception lastEx = null;

            do
            {
                var channelFactory = new DefaultWampChannelFactory();
                var channel = channelFactory.CreateJsonChannel(_pushApiAddress, "realm1");
                channel.Open().GetAwaiter().GetResult();

                lastEx = null;
                var restartTS = new CancellationTokenSource();

                var tickerSubject = channel.RealmProxy.Services.GetSubject("ticker");
                using (var subscr = tickerSubject.Subscribe(
                    onNext: evt =>
                    {
                        var currencyPair = evt.Arguments[0].Deserialize<string>();
                        if (currencyPairFilter == null || currencyPair == currencyPairFilter)
                        {
                            var currencyPairParts = currencyPair.Split('_');

                            var ticker = new Ticker
                            {
                                BaseCurrency = currencyPairParts[0],
                                Currency = currencyPairParts[1],
                                Last = evt.Arguments[1].Deserialize<decimal>(),
                                LowestAsk = evt.Arguments[2].Deserialize<decimal>(),
                                HighestBid = evt.Arguments[3].Deserialize<decimal>(),
                                PercentageChange = evt.Arguments[4].Deserialize<decimal>(),
                                BaseVolume = evt.Arguments[5].Deserialize<decimal>(),
                                QuoteVolume = evt.Arguments[6].Deserialize<decimal>(),
                                IsFrozen = evt.Arguments[7].Deserialize<int>() == 1,
                                TwentyFourHrHigh = evt.Arguments[8].Deserialize<decimal>(),
                                TwentyFourHrLow = evt.Arguments[9].Deserialize<decimal>(),
                                Timestamp = DateTime.UtcNow,
                            };

                            onTicker(ticker);
                        }
                    },
                    onError: (ex) =>
                    {
                        lastEx = ex;
                        restartTS.Cancel();
                        onException(ex);
                    },
                    onCompleted: () =>
                    {
                        // Treat as exception (we don't want a sudden complete)
                        lastEx = new Exception("Subscription unexpectedly completed");
                        restartTS.Cancel();
                        onException(lastEx);
                    }
                    ))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (restartTS.Token.IsCancellationRequested)
                            break;

                        Task.Delay(100, cancellationToken).GetAwaiter().GetResult();
                    }
                }
            } while (lastEx != null); // Keep restarting while errors occur
        }

        public Dictionary<string,BalanceDetails> GetBalance(string currency = null)
        {
            var balances = ExecuteRequest<Dictionary<string, BalanceDetails>>("returnCompleteBalances");
            if (currency != null)
                balances = new Dictionary<string, BalanceDetails> { { currency, balances[currency] } };
            return balances;
        }

        public Dictionary<string,OpenOrder> GetOpenOrders(string currencyPair = null)
        {
            var openOrders = ExecuteRequest<Dictionary<string, OpenOrder>>("returnOpenOrders", new Dictionary<string, string> { {"currencyPair", currencyPair ?? "all" } });
            return openOrders;
        }

        public OrderAndTrades PlaceOrder(string currencyPair, decimal rate, decimal amount, bool isBuy)
        {
            var order = ExecuteRequest<OrderAndTrades>(isBuy ? "buy" : "sell", new Dictionary<string, string>
            {
                { "currencyPair", currencyPair },
                { "rate", rate.ToString(CultureInfo.InvariantCulture) },
                { "amount", amount.ToString(CultureInfo.InvariantCulture) },
                { "immediateOrCancel", "1" }, // Force
            });
            return order;
        }
        public void CancelOrder(string orderNumber)
        {
            var order = ExecuteRequest<string>("cancelOrder", new Dictionary<string, string>
            {
                { "orderNumber", orderNumber },
            });
        }

        public void Disconnect()
        {
            //_channel = null;
        }

        public void Dispose()
        {
            //if (_channel != null)
            //    Disconnect();
        }

        private T ExecuteRequest<T>(string command, Dictionary<string, string> arguments = null)
        {
            using (var client = new HttpClient { BaseAddress = new Uri(_orderApiAddress) })
            {
                var formParams = new Dictionary<string, string>
                {
                    { "command", "returnCompleteBalances" },
                    { "nonce", $"{DateTime.UtcNow.Ticks}" }
                };
                if (arguments != null)
                    arguments.ToList().ForEach(kvp => formParams.Add(kvp.Key, kvp.Value));

                var content = new FormUrlEncodedContent(formParams);
                var hash = new HMACSHA512(_poloSecret).ComputeHash(content.ReadAsByteArrayAsync().GetAwaiter().GetResult());

                var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_orderApiAddress, UriKind.Absolute));
                request.Headers.Add("Key", _poloKey);
                request.Headers.Add("Sign", String.Join(string.Empty, hash.Select(b => b.ToString("X2"))));
                request.Content = content;
                var response = client.SendAsync(request).GetAwaiter().GetResult();
                var contentAsString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonConvert.DeserializeObject<T>(contentAsString);
            }
        }
    }
}
