using Newtonsoft.Json.Linq;
using Poloniex.APIClient.Models;
using System;
using System.Collections.Generic;
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

        public PoloniexAPIClient()
            : this("wss://api.poloniex.com")
        {
        }

        public PoloniexAPIClient(string pushApiAddress)
        {
            _pushApiAddress = pushApiAddress;
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

        public void Disconnect()
        {
            //_channel = null;
        }

        public void Dispose()
        {
            //if (_channel != null)
            //    Disconnect();
        }
    }
}
