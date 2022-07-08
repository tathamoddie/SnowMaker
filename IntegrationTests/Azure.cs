using Azure.Storage.Blobs;
using NUnit.Framework;
using SnowMaker;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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
            return new BlobOptimisticDataStore("UseDevelopmentStorage=true", scope.ContainerName, scope.Options);
        }

        public class TestScope : ITestScope
        {
            readonly BlobContainerClient blobContainerClient;

            public TestScope(string storageConnectionString)
            {
                var ticks = DateTime.UtcNow.Ticks;
                IdScopeName = string.Format("snowmakertest{0}", ticks);
                ContainerName = string.Format("snowmakertest{0}", ticks);

                blobContainerClient = new BlobContainerClient(storageConnectionString, ContainerName, Options);
            }

            public string IdScopeName { get; private set; }
            public string ContainerName { get; private set; }
            internal readonly BlobClientOptions Options = default;// new BlobClientOptions(BlobClientOptions.ServiceVersion.V2020_12_06);

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
                try
                {
                    blobContainerClient.Delete();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }
    }
}
