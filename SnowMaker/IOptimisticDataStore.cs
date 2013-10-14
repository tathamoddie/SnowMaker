namespace SnowMaker
{
    public interface IOptimisticDataStore
    {
        /// <summary>
        /// Marks the next <paramref name="batchSize"/> ids starting at the next available id
        /// </summary>
        /// <param name="blockName"></param>
        /// <param name="batchSize"></param>
        /// <returns>The first available id in the given batch size, or -1 if the call was unable to lock the store for exclusive access</returns>
        long GetNextBatch(string blockName, int batchSize);
    }
}
