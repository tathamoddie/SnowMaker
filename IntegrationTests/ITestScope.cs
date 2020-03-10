using System;

namespace IntegrationTests
{
    public interface ITestScope : IDisposable
    {
        string IdScopeName { get; }
        string ReadCurrentPersistedValue();
    }
}