using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Poloniex.APIClient.Repositories
{
    public abstract class DbRepo : BaseRepo, IDisposable
    {
        static DbRepo()
        {
            // Add custom type handler to treat all DateTimes as UTC
            SqlMapper.AddTypeHandler(new DateTimeUtcTypeHandler());
        }

        #region Inner types
        public enum TranState : byte
        {
            Unchanged = 0,
            Committed = 1,
            Rollbacked = 2,
        }

        #endregion

        protected readonly IDbConnection _conn;
        protected IDbTransaction _runningTransaction;
        protected int _tranCount = 0;
        protected object _tranLock = new object();
        protected TranState _tranState = TranState.Unchanged;

        /// <summary>
        /// Creates a new DbEventingRepo.
        /// </summary>
        /// <param name="conn">IDbConnection to use. If not yet opened, it will be opened here.</param>
        public DbRepo(IDbConnection conn, TimeSpan commandTimeout)
        {
            _conn = conn;
            CommandTimeout = commandTimeout;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// IDispose implementation
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_conn != null)
                {
                    if (_conn.State != ConnectionState.Closed)
                    {
                        try
                        {
                            _conn.Close();
                        }
                        catch (Exception) { } // Swallow, can't help it anyway
                    }
                    _conn.Dispose();
                }
            }
        }

        #region Connection Management
        /// <summary>
        /// Ensures the provided connection is available and open.
        /// </summary>
        /// <returns></returns>
        protected virtual async Task EnsureConnectionReady()
        {
            if (_conn == null)
                throw new InvalidOperationException("No connection available");

            var dbConn = _conn as DbConnection; // If _conn appears to be a (derived) DbConnection, we cast it, since it provides some async methods
            if (_conn.State == ConnectionState.Broken)
            {
                // When broken, just close and reopen again, might do the job
                _conn.Close();
                if (dbConn != null)
                    await dbConn.OpenAsync().ConfigureAwait(false);
                else
                    _conn.Open();
            }
            else if (_conn.State == ConnectionState.Closed)
            {
                if (dbConn != null)
                    await dbConn.OpenAsync().ConfigureAwait(false);
                else
                    _conn.Open();
            }
        }

        /// <summary>
        /// Indicates whether the repo supports running parallel queries on a single connection. Default=false.
        /// </summary>
        protected override bool ParallelQueriesSupport { get { return false; } }

        /// <summary>
        /// CommandTimeout to use. Set by constructor, but can be modified.
        /// </summary>
        public virtual TimeSpan CommandTimeout { get; set; }

        #endregion

        #region Transactions
        /// <summary>
        /// Starts a new transaction.
        /// NB: Transactions can be nested.
        /// </summary>
        protected async override Task BeginTransactionAsync()
        {
            await EnsureConnectionReady().ConfigureAwait(false);
            lock (_tranLock)
            {
                if (_runningTransaction == null)
                {
                    _runningTransaction = _conn.BeginTransaction(IsolationLevel.RepeatableRead); // Prevents deadlocks and, in case of MsSql, skipping/overtaking of events
                    _tranState = TranState.Unchanged;
                }
                _tranCount++;
            }
        }

        /// <summary>
        /// Rolls back the the transaction and disposes it.
        /// Make sure there are no parallel threads/tasks still using the transaction!
        /// </summary>
        protected override async Task RollbackTransactionAsync()
        {
            if (_runningTransaction == null) // Check before waiting for lock to prevent unnessecary locks
                throw new ArgumentException($"No running transaction found");

            await EnsureConnectionReady().ConfigureAwait(false);
            lock (_tranLock)
            {
                if (_runningTransaction == null)
                    throw new ArgumentException($"No running transaction found");

                if (_tranState != TranState.Rollbacked) // Nothing to do if already rolled back
                {
                    if (_runningTransaction.Connection != null) // Would be weird, since it's only cleared after a rollback or commit (on SqlServer)
                        _runningTransaction.Rollback(); // Rollback immediately

                    _tranState = TranState.Rollbacked;
                }

                _tranCount--;
                if (_tranCount == 0)
                {
                    _runningTransaction.Dispose();
                    _runningTransaction = null;
                }
            }
        }

        /// <summary>
        /// Commits the transaction and disposes it.
        /// Make sure there are no parallel threads/tasks still using the transaction!
        /// </summary>
        protected override async Task CommitTransactionAsync()
        {
            if (_runningTransaction == null) // Check before waiting for lock to prevent unnessecary locks
                throw new ArgumentException($"No running transaction found");

            await EnsureConnectionReady().ConfigureAwait(false);
            lock (_tranLock)
            {
                if (_runningTransaction == null)
                    throw new ArgumentException($"No running transaction found");

                if (_tranState == TranState.Rollbacked)
                    throw new InvalidOperationException("Transaction has already been rolled back");

                _tranState = TranState.Committed;

                _tranCount--;
                if (_tranCount == 0)
                {
                    // We got till the highest level, so no perform the actual action on the transaction
                    _runningTransaction.Commit();
                    _runningTransaction.Dispose();
                    _runningTransaction = null;
                }
            }
        }

        /// <summary>
        /// Transacted execution; if a transaction was started, the execute will take place on/in it
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        protected async virtual Task<int> TranExecuteAsync(string sql, object param = null, int? commandTimeout = null)
        {
            await EnsureConnectionReady().ConfigureAwait(false);
            var query = sql.ToLowerInvariant(); // MySql requires lower-case table names. This way the queries do not need to change, which is more readable.
            return await _conn.ExecuteAsync(query,
                param: param,
                transaction: _runningTransaction,
                commandTimeout: commandTimeout.GetValueOrDefault((int)CommandTimeout.TotalSeconds)).ConfigureAwait(false);
        }

        /// <summary>
        /// Transacted query; if a transaction was started, the query will take place on/in it
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        protected async virtual Task<IEnumerable<T>> TranQueryAsync<T>(string sql, object param = null, int? commandTimeout = null)
        {
            await EnsureConnectionReady().ConfigureAwait(false);
            var query = sql.ToLowerInvariant(); // MySql requires lower-case table names. This way the queries do not need to change, which is more readable.
            return await _conn.QueryAsync<T>(query,
                param: param,
                transaction: _runningTransaction,
                commandTimeout: commandTimeout.GetValueOrDefault((int)CommandTimeout.TotalSeconds)).ConfigureAwait(false);
        }
        #endregion

        #region Repo-specific DB-implementation

        /// <summary>
        /// Depending on the type of DbException, the specific repo can determine if a retry of the DB-call is usefull.
        /// </summary>
        /// <param name="dbEx">The DbException</param>
        /// <param name="attempts">The nr of attempts tried so far</param>
        /// <returns></returns>
        protected virtual bool CanRetry(DbException dbEx, int attempts)
        {
            return false;
        }

        /// <summary>
        /// Function signature for getting the latest autoincrement value
        /// </summary>
        public abstract string GetLastAutoIncrementValue { get; }
        #endregion
    }
}
