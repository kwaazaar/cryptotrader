using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Poloniex.APIClient.Repositories
{
    public abstract class BaseRepo
    {
        /// <summary>
        /// Min datetime to use (Sql Server does not support any lower value than this)
        /// </summary>
        public static DateTime MinDateTime { get { return new DateTime(1753, 1, 1); } }

        /// <summary>
        /// Max datetime to use
        /// </summary>
        public static DateTime MaxDateTime { get { return new DateTime(9999, 12, 31); } }

        protected abstract bool ParallelQueriesSupport { get; }
        protected abstract Task BeginTransactionAsync();
        protected abstract Task RollbackTransactionAsync();
        protected abstract Task CommitTransactionAsync();
    }
}
