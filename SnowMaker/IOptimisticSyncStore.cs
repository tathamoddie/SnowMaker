namespace SnowMaker
{
    public interface IOptimisticSyncStore
    {
        string GetData();
        bool TryOptimisticWrite(string data);
    }
}
