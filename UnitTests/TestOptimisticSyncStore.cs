using System.Threading;

namespace SnowMaker.UnitTests
{
    public class TestOptimisticSyncStore : IOptimisticSyncStore
    {
        int tryWriteCount;

        public string GetDataValue { get; set; }
        public string SetDataValue { get; private set; }
        public bool TryWriteResult { get; set; }
        public int TryWriteCount { get { return tryWriteCount; } }

        public string GetData()
        {
            return GetDataValue;
        }

        public bool TryOptimisticWrite(string data)
        {
            Interlocked.Increment(ref tryWriteCount);
            SetDataValue = data;
            return TryWriteResult;
        }
    }
}