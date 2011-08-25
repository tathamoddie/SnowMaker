using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        [Test]
        public void ShouldReturnIdsAcrossMultipleGenerators()
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
                var generatedIds = new[]
                {
                    generator1.NextId(testScope.IdScopeName), //0
                    generator1.NextId(testScope.IdScopeName), //1
                    generator1.NextId(testScope.IdScopeName), //2
                    generator2.NextId(testScope.IdScopeName), //3
                    generator1.NextId(testScope.IdScopeName), //6
                    generator2.NextId(testScope.IdScopeName), //4
                    generator2.NextId(testScope.IdScopeName), //5
                    generator2.NextId(testScope.IdScopeName), //9
                    generator1.NextId(testScope.IdScopeName), //7
                    generator1.NextId(testScope.IdScopeName)  //8
                };

                // Assert
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2, 3, 6, 4 , 5, 9, 7, 8 },
                    generatedIds);
            }
        }

        [Test]
        public void ShouldSupportUsingOneGeneratorFromMultipleThreads()
        {
            // Arrange
            var account = CloudStorageAccount.DevelopmentStorageAccount;
            using (var testScope = new TestScope(account))
            {
                var store = new BlobOptimisticDataStore(account, testScope.ContainerName);
                var generator = new UniqueIdGenerator(store) { BatchSize = 1000 };
                const int testLength = 10000;

                // Act
                var generatedIds = new ConcurrentQueue<long>();
                var threadIds = new ConcurrentQueue<int>();
                var scopeName = testScope.IdScopeName;
                Parallel.For(
                    0,
                    testLength,
                    new ParallelOptions { MaxDegreeOfParallelism = 10 },
                    i =>
                    {
                        generatedIds.Enqueue(generator.NextId(scopeName));
                        threadIds.Enqueue(Thread.CurrentThread.ManagedThreadId);
                    });

                // Assert we generated the right count of ids
                Assert.AreEqual(testLength, generatedIds.Count);

                // Assert there were no duplicates
                Assert.IsFalse(generatedIds.GroupBy(n => n).Where(g => g.Count() != 1).Any());

                // Assert we used multiple threads
                var uniqueThreadsUsed = threadIds.Distinct().Count();
                if (uniqueThreadsUsed == 1)
                    Assert.Inconclusive("The test failed to actually utilize multiple threads");
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
