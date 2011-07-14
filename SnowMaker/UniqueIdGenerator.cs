using System;
using System.Threading;

namespace SnowMaker
{
    public class UniqueIdGenerator : IUniqueIdGenerator
    {
        readonly object padLock = new object();
        readonly int rangeSize;
        readonly int maxRetries;
        readonly IOptimisticSyncStore optimisticSyncStore;
        Int64 lastId;
        Int64 upperLimit;

        public UniqueIdGenerator(
            IOptimisticSyncStore optimisticSyncStore,
            int rangeSize = 1000,
            int maxRetries = 25)
        {
            this.rangeSize = rangeSize;
            this.maxRetries = maxRetries;
            this.optimisticSyncStore = optimisticSyncStore;
            // need to load the initial configuration
            UpdateFromSyncStore();
        }

        public long NextId()
        {
            lock (padLock)
            {
                if (lastId == upperLimit)
                {
                    UpdateFromSyncStore();
                }
                return Interlocked.Increment(ref lastId);
            }
        }

        private void UpdateFromSyncStore()
        {
            var retryCount = 0;

            // maxRetries + 1 because the first run isn't a 're'try.
            while (retryCount < maxRetries + 1)
            {
                var data = optimisticSyncStore.GetData();

                if (!Int64.TryParse(data, out lastId))
                {
                    throw new Exception(string.Format(
                       "Data '{0}' in storage was corrupt and could not be parsed as an Int64"
                       , data));
                }

                upperLimit = lastId + rangeSize;

                if (optimisticSyncStore.TryOptimisticWrite(upperLimit.ToString()))
                {
                    // update succeeded
                    return;
                }

                retryCount++;
                // update failed, go back around the loop
            }

            throw new Exception(string.Format(
                "Failed to update the OptimisticSyncStore after {0} attempts",
                retryCount));
        }
    }
}
