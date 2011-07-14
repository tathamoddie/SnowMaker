using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using System.Threading;

namespace Evolve.WindowsAzure
{
    /// <summary>
    /// Used to generate simple, unique identifiers across multiple environments, processes and/or threads. Requires a global data
    /// store that can be used to store the last upper limit (must implement the IOptimisticSyncStore interface). Contention is reduced
    /// by allocating ranges to each instance of the UniqueIdGenerator. The RangeSize should increase proportionally with the fre
    /// </summary>
    public class UniqueIdGenerator
    {
        private readonly object _padLock = new object();
        private Int64 _lastId;
        private Int64 _upperLimit;
        private int _rangeSize;
        private int _maxRetries;
        private IOptimisticSyncStore _optimisticSyncStore;

        public UniqueIdGenerator(
            IOptimisticSyncStore optimisticSyncStore,
            int rangeSize = 1000,
            int maxRetries = 25)
        {
            _rangeSize = rangeSize;
            _maxRetries = maxRetries;
            _optimisticSyncStore = optimisticSyncStore;
            // need to load the initial configuration
            UpdateFromSyncStore();
        }

        /// <summary>
        /// Fetches the next available unique ID
        /// </summary>
        /// <returns></returns>
        public Int64 NextId()
        {
            lock (_padLock)
            {
                if (_lastId == _upperLimit)
                {
                    UpdateFromSyncStore();
                }
                return Interlocked.Increment(ref _lastId);
            }
        }

        private void UpdateFromSyncStore()
        {
            int retryCount = 0;

            // maxRetries + 1 because the first run isn't a 're'try.
            while (retryCount < _maxRetries + 1)
            {
                string data = _optimisticSyncStore.GetData();

                if (!Int64.TryParse(data, out _lastId))
                {
                    throw new Exception(string.Format(
                       "Data '{0}' in storage was corrupt and could not be parsed as an Int64"
                       , data));
                }

                _upperLimit = _lastId + _rangeSize;

                if (_optimisticSyncStore.TryOptimisticWrite(_upperLimit.ToString()))
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
