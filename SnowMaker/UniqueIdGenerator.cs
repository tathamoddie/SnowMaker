using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace SnowMaker
{
    public class UniqueIdGenerator : IUniqueIdGenerator
    {
        readonly IOptimisticDataStore optimisticDataStore;

        readonly IDictionary<string, ScopeState> states = new Dictionary<string, ScopeState>();
        readonly object statesLock = new object();

        int batchSize = 100;
        int maxWriteAttempts = 25;

        public UniqueIdGenerator(IOptimisticDataStore optimisticDataStore)
        {
            this.optimisticDataStore = optimisticDataStore;
        }

        public int BatchSize
        {
            get { return batchSize; }
            set { batchSize = value; }
        }

        public int MaxWriteAttempts
        {
            get { return maxWriteAttempts; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("value", maxWriteAttempts, "MaxWriteAttempts must be a positive number.");

                maxWriteAttempts = value;
            }
        }

        public long NextId(string scopeName)
        {
            var state = GetScopeState(scopeName);

            lock (state.IdGenerationLock)
            {
                if (state.LastId == state.HighestIdAvailableInBatch)
                    UpdateFromSyncStore(scopeName, state);

                return Interlocked.Increment(ref state.LastId);
            }
        }

        ScopeState GetScopeState(string scopeName)
        {
            return states.GetValue(
                scopeName,
                statesLock,
                () => new ScopeState());
        }

        void UpdateFromSyncStore(string scopeName, ScopeState state)
        {
            var writesAttempted = 0;

            while (writesAttempted < maxWriteAttempts)
            {
                long id = optimisticDataStore.GetNextBatch(scopeName, batchSize);
                if (id != -1)
                {
                    state.LastId = id - 1;
                    state.HighestIdAvailableInBatch = id - 1 + batchSize;
                    return;
                }
                writesAttempted++;
            }

            throw new UniqueIdGenerationException(string.Format(
                "Failed to update the data store after {0} attempts. This likely represents too much contention against the store. Increase the batch size to a value more appropriate to your generation load.",
                writesAttempted));
        }
    }
}
