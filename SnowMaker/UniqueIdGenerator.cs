using System;
using System.Threading;

namespace SnowMaker
{
    public class UniqueIdGenerator : IUniqueIdGenerator
    {
        readonly int rangeSize;
        readonly int maxRetries;
        readonly IOptimisticDataStore optimisticDataStore;
        readonly string scopeName;

        readonly ScopeState state = new ScopeState();

        public UniqueIdGenerator(
            IOptimisticDataStore optimisticDataStore,
            string scopeName,
            int rangeSize = 100,
            int maxRetries = 25)
        {
            this.rangeSize = rangeSize;
            this.maxRetries = maxRetries;
            this.optimisticDataStore = optimisticDataStore;
            this.scopeName = scopeName;
        }

        public long NextId()
        {
            lock (state.IdGenerationLock)
            {
                if (state.LastId == state.UpperLimit)
                {
                    UpdateFromSyncStore();
                }
                return Interlocked.Increment(ref state.LastId);
            }
        }

        private void UpdateFromSyncStore()
        {
            var retryCount = 0;

            // maxRetries + 1 because the first run isn't a 're'try.
            while (retryCount < maxRetries + 1)
            {
                var data = optimisticDataStore.GetData(scopeName);

                if (!Int64.TryParse(data, out state.LastId))
                {
                    throw new Exception(string.Format(
                       "Data '{0}' in storage was corrupt and could not be parsed as an Int64"
                       , data));
                }

                state.UpperLimit = state.LastId + rangeSize;

                if (optimisticDataStore.TryOptimisticWrite(scopeName, state.UpperLimit.ToString()))
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
