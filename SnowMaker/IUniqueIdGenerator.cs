namespace SnowMaker
{
    public interface IUniqueIdGenerator
    {
        long NextId(string scopeName);
    }
}