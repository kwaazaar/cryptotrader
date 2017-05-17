using Poloniex.APIClient.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poloniex.APIClient.Repositories
{
    public class CryptoTradingRepo : DbRepo
    {
        private readonly SqlConnectionStringBuilder _connStringBuilder;
        public CryptoTradingRepo(IDbConnection conn, TimeSpan commandTimeout)
            : base(conn, commandTimeout)
        {
            _connStringBuilder = new SqlConnectionStringBuilder(conn.ConnectionString);
        }

        #region Base class implements
        protected override bool CanRetry(DbException dbEx, int attempts)
        {
            var sqlEx = dbEx as SqlException;
            if (sqlEx != null && sqlEx.Number == 1205 && attempts < 3) // After 3 attempts give up deadlocks
                return true;
            else
                return base.CanRetry(dbEx, attempts);
        }

        protected override bool ParallelQueriesSupport { get { return _connStringBuilder.MultipleActiveResultSets; } } // 'MARS' must be enabled in the connectionstring

        public override string GetLastAutoIncrementValue
        {
            get { return "SCOPE_IDENTITY()"; }
        }
        #endregion

        public async Task<IEnumerable<string>> GetCurrencies()
        {
            var query = "SELECT c.Currency FROM Currency c";

            return await TranQueryAsync<string>(query, new Dictionary<string, object>
            {
            }).ConfigureAwait(false);
        }

        public async Task<int> StoreTicker(Ticker t)
        {
            var query = "INSERT INTO Ticker (Currency, BaseCurrency, Timestamp, Last, LowestAsk, HighestBid, PercentChange, BaseVolume, QuoteVolume, IsFrozen, TwentyFourHrHigh, TwentyFourHrLow, CurvePct) "
                + "VALUES (@currency, @baseCurrency, @timestamp, @last, @lowestAsk, @highestBid, @percentageChange, @baseVolume, @quoteVolume, @isFrozen, @twentyFourHrHigh, @twentyFourHrLow, @curvePct)";

            return await TranExecuteAsync(query, t).ConfigureAwait(false);
        }
        public async Task<IEnumerable<Ticker>> GetTicker(string currency, string baseCurrency, DateTime periodStartUtc, DateTime? periodEndUtc)
        {
            var query = "SELECT * FROM Ticker " +
                "WHERE Currency = @currency AND BaseCurrency = @baseCurrency " +
                "AND timestamp BETWEEN @periodStartUtc AND @periodEndUtc";

            return await TranQueryAsync<Ticker>(query, new Dictionary<string, object>
            {
                { "@currency", currency },
                { "@baseCurrency", baseCurrency },
                { "@periodStartUtc", periodStartUtc },
                { "@periodEndUtc", periodEndUtc },
            }).ConfigureAwait(false);
        }

        public async Task<IEnumerable<Order>> GetOrders(string currency, DateTime periodStartUtc, DateTime periodEndUtc)
        {
            var query = "SELECT * FROM Order " +
                "WHERE Currency = @currency AND timestamp BETWEEN @periodStartUtc AND @periodEndUtc";

            return await TranQueryAsync<Order>(query, new Dictionary<string, object>
            {
                { "@currency", currency },
                { "@periodStartUtc", periodStartUtc },
                { "@periodEndUtc", periodEndUtc },
            }).ConfigureAwait(false);
        }

        public async Task<Order> GetLastOrder(string currency)
        {
            var query = "SELECT TOP 1 * FROM Order " +
                "WHERE Currency = @currency ORDER BY timestamp DESC";

            var orders = await TranQueryAsync<Order>(query, new Dictionary<string, object>
            {
                { "@currency", currency },
            }).ConfigureAwait(false);

            return orders.FirstOrDefault();
        }

        public async Task<int> AddOrder(Order o)
        {
            var query = "INSERT INTO Order (Currency, BaseCurrency, Amount, Price, Fee, TotalPrice, Timestamp) " +
                "VALUES (@currency, @baseCurrency, @amount, @price, @fee, @totalprice, @timestamp)";

            return await TranExecuteAsync(query, o).ConfigureAwait(false);
        }
    }
}
