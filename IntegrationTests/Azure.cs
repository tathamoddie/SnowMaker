using System;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using NUnit.Framework;
using SnowMaker;

namespace IntegrationTests.cs
{
    [TestFixture]
    public class Azure : Scenarios<Azure.TestScope>
    {
        readonly CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;

        protected override TestScope BuildTestScope()
        {
            return new TestScope(CloudStorageAccount.DevelopmentStorageAccount);
        }

        protected override IOptimisticDataStore BuildStore(TestScope scope)
        {
            return new BlobOptimisticDataStore(storageAccount, scope.ContainerName);
        }

        public class TestScope : ITestScope
        {
            readonly CloudBlobClient blobClient;

            public TestScope(CloudStorageAccount account)
            {
                var ticks = DateTime.UtcNow.Ticks;
                IdScopeName = string.Format("snowmakertest{0}", ticks);
                ContainerName = string.Format("snowmakertest{0}", ticks);

                blobClient = account.CreateCloudBlobClient();
            }

            public string IdScopeName { get; private set; }
            public string ContainerName { get; private set; }

            public string ReadCurrentPersistedValue()
            {
                var blobContainer = blobClient.GetContainerReference(ContainerName);
                var blob = blobContainer.GetBlobReference(IdScopeName);
                return blob.DownloadText();
            }

            public void Dispose()
            {
                var blobContainer = blobClient.GetContainerReference(ContainerName);
                blobContainer.Delete();
            }
        }
    }
}
