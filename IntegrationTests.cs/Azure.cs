using System;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using NUnit.Framework;
using SnowMaker;

namespace IntegrationTests.cs
{
    [TestFixture]
    public class Azure
    {
        [Test]
        public void FirstIdInNewScopeShouldBeZero()
        {
            // Arrange
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            using (var testScope = new TestScope(account))
            {
                var store = new BlobOptimisticDataStore(account, testScope.ContainerName);
                var generator = new UniqueIdGenerator(store);
                
                // Act
                var generatedId = generator.NextId(testScope.IdScopeName);

                // Assert
                Assert.AreEqual(0, generatedId);
            }
        }

        public class TestScope : IDisposable
        {
            readonly CloudStorageAccount account;

            public TestScope(CloudStorageAccount account)
            {
                this.account = account;
                
                var ticks = DateTime.UtcNow.Ticks;
                IdScopeName = string.Format("snowmakertest{0}", ticks);
                ContainerName = string.Format("snowmakertest{0}", ticks);
            }

            public string IdScopeName { get; private set; }
            public string ContainerName { get; private set; }

            public void Dispose()
            {
                var blobClient = account.CreateCloudBlobClient();
                var blobContainer = blobClient.GetContainerReference(ContainerName);
                blobContainer.Delete();
            }
        }
    }
}
