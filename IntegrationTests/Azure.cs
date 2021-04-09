using System;
using NUnit.Framework;
using SnowMaker;
using System.Text;
using System.IO;
using Azure.Storage.Blobs;

namespace IntegrationTests
{
    [TestFixture]
    public class Azure : Scenarios<Azure.TestScope>
    {
        protected override TestScope BuildTestScope()
        {
            return new TestScope("UseDevelopmentStorage=true");
        }

        protected override IOptimisticDataStore BuildStore(TestScope scope)
        {
            return new BlobOptimisticDataStore("UseDevelopmentStorage=true", scope.ContainerName);
        }

        public class TestScope : ITestScope
        {
            readonly BlobContainerClient blobContainerClient;

            public TestScope(string storageConnectionString)
            {
                var ticks = DateTime.UtcNow.Ticks;
                IdScopeName = string.Format("snowmakertest{0}", ticks);
                ContainerName = string.Format("snowmakertest{0}", ticks);

                blobContainerClient = new BlobContainerClient(storageConnectionString, ContainerName);
            }

            public string IdScopeName { get; private set; }
            public string ContainerName { get; private set; }

            public string ReadCurrentPersistedValue()
            {
                var blob = blobContainerClient.GetBlobClient(IdScopeName);
                using (var stream = new MemoryStream())
                {
                    blob.DownloadTo(stream);
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }

            public void Dispose()
            {
                blobContainerClient.Delete();
            }
        }
    }
}
