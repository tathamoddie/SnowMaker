using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Evolve.WindowsAzure
{
    public interface IOptimisticSyncStore
    {
        string GetData();
        bool TryOptimisticWrite(string data);
    }
}
