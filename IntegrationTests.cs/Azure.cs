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
        public void ShouldReturnZeroForFirstIdInNewScope()
        {
            // Arrange
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            using (var testScope = new TestScope(account))
            {
                var store = new BlobOptimisticDataStore(account, testScope.ContainerName);
                var generator = new UniqueIdGenerator(store) {BatchSize = 3};
                
                // Act
                var generatedId = generator.NextId(testScope.IdScopeName);

                // Assert
                Assert.AreEqual(0, generatedId);
            }
        }

        [Test]
        public void ShouldInitializeBlobForFirstIdInNewScope()
        {
            // Arrange
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            using (var testScope = new TestScope(account))
            {
                var store = new BlobOptimisticDataStore(account, testScope.ContainerName);
                var generator = new UniqueIdGenerator(store) {BatchSize = 3};

                // Act
                generator.NextId(testScope.IdScopeName); //0

                // Assert
                Assert.AreEqual("3", testScope.ReadCurrentBlobValue());
            }
        }

        [Test]
        public void ShouldNotUpdateBlobAtEndOfBatch()
        {
            // Arrange
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            using (var testScope = new TestScope(account))
            {
                var store = new BlobOptimisticDataStore(account, testScope.ContainerName);
                var generator = new UniqueIdGenerator(store) { BatchSize = 3 };

                // Act
                generator.NextId(testScope.IdScopeName); //0
                generator.NextId(testScope.IdScopeName); //1
                generator.NextId(testScope.IdScopeName); //2

                // Assert
                Assert.AreEqual("3", testScope.ReadCurrentBlobValue());
            }
        }

        [Test]
        public void ShouldUpdateBlobWhenGeneratingNextIdAfterEndOfBatch()
        {
            // Arrange
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            using (var testScope = new TestScope(account))
            {
                var store = new BlobOptimisticDataStore(account, testScope.ContainerName);
                var generator = new UniqueIdGenerator(store) { BatchSize = 3 };

                // Act
                generator.NextId(testScope.IdScopeName); //0
                generator.NextId(testScope.IdScopeName); //1
                generator.NextId(testScope.IdScopeName); //2
                generator.NextId(testScope.IdScopeName); //3

                // Assert
                Assert.AreEqual("6", testScope.ReadCurrentBlobValue());
            }
        }

        [Test]
        public void ShouldReturnIdsFromThirdBatchIfSecondBatchTakenByAnotherGenerator()
        {
            // Arrange
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            using (var testScope = new TestScope(account))
            {
                var store1 = new BlobOptimisticDataStore(account, testScope.ContainerName);
                var generator1 = new UniqueIdGenerator(store1) { BatchSize = 3 };
                var store2 = new BlobOptimisticDataStore(account, testScope.ContainerName);
                var generator2 = new UniqueIdGenerator(store2) { BatchSize = 3 };

                // Act
                generator1.NextId(testScope.IdScopeName); //0
                generator1.NextId(testScope.IdScopeName); //1
                generator1.NextId(testScope.IdScopeName); //2
                generator2.NextId(testScope.IdScopeName); //3
                var lastId = generator1.NextId(testScope.IdScopeName); //6

                // Assert
                Assert.AreEqual(6, lastId);
            }
        }

        public class TestScope : IDisposable
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

            public string ReadCurrentBlobValue()
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
