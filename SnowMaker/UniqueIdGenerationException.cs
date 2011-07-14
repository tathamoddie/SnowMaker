using System;

namespace SnowMaker
{
    public class UniqueIdGenerationException : Exception
    {
        public UniqueIdGenerationException(string message)
            : base(message)
        {
        }
    }
}