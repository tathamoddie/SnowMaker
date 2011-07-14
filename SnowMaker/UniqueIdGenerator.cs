using System;
using System.Threading;

namespace SnowMaker
{
    public class UniqueIdGenerator : IUniqueIdGenerator
    {
        readonly object idGenerationLock = new object();
        readonly int rangeSize;
        readonly int maxRetries;
        readonly IOptimisticDataStore optimisticDataStore;
        Int64 lastId;
        Int64 upperLimit;

        public UniqueIdGenerator(
            IOptimisticDataStore optimisticDataStore,
            int rangeSize = 1000,
            int maxRetries = 25)
        {
            this.rangeSize = rangeSize;
            this.maxRetries = maxRetries;
            this.optimisticDataStore = optimisticDataStore;
        }

        public long NextId()
        {
            lock (idGenerationLock)
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
                var data = optimisticDataStore.GetData();

                if (!Int64.TryParse(data, out lastId))
                {
                    throw new Exception(string.Format(
                       "Data '{0}' in storage was corrupt and could not be parsed as an Int64"
                       , data));
                }

                upperLimit = lastId + rangeSize;

                if (optimisticDataStore.TryOptimisticWrite(upperLimit.ToString()))
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
