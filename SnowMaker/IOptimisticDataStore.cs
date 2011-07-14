namespace SnowMaker
{
    public interface IOptimisticDataStore
    {
        string GetData();
        bool TryOptimisticWrite(string data);
    }
}
