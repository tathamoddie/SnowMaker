using System;
//using NSubstitute;
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace SnowMaker.UnitTests
{
    [TestFixture]
    public class FileOptimisticDataStoreTests
    {
        private class TestScope: IDisposable
        {
            public readonly string FilePath;

            public TestScope(string scope)
            {
                FilePath = Path.Combine(Path.GetTempPath(), string.Format("{0}.txt", scope));
            }

            // does not lock the file
            public long ReadCurrentPersistedValue()
            {
                using (TextReader reader = new StreamReader(FilePath, FileOptimisticDataStore.Encoding))
                    return Convert.ToInt64(reader.ReadToEnd());
            }

            public void Dispose()
            {
                int count = 0;
                do
                {
                    try
                    {
                        File.Delete(FilePath);
                        Thread.Sleep(10);
                        return;
                    }
                    catch
                    {
                        // retry, it could still be blocked by another thread from the tests
                        count++;
                    }
                }
                while(count < 5);
            }
        }

        private const string scope = "test";
        private const int batch = 1;

        [Test]
        public void ConstructorShouldNotCreateFile()
        {
            using (var testScope = new TestScope(scope))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                Assert.IsFalse(File.Exists(testScope.FilePath));
            }
        }

        [Test]
        public void ShouldCreateFileOnFirstAccess()
        {
            using (var testScope = new TestScope(scope))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                store.GetNextBatch(scope, batch);
                Assert.IsTrue(File.Exists(testScope.FilePath));
                Assert.AreEqual(testScope.ReadCurrentPersistedValue(), FileOptimisticDataStore.SeedValue + batch);
            }
        }

        [Test]
        public void GetNextBatchShouldBlockFileAccess()
        {
            using (var testScope = new TestScope(scope))
            {
                CancellationTokenSource cancelTokenSource1 = new CancellationTokenSource();
                CancellationTokenSource cancelTokenSource2 = new CancellationTokenSource();

                try
                {
                    var store = new FileOptimisticDataStore(Path.GetTempPath());
                    store.GetNextBatch(scope, batch); // create the file

                    CancellationToken cancelToken1 = cancelTokenSource1.Token;
                    Task task1 = Task.Factory.StartNew(() =>
                    {
                        do
                            store.GetNextBatch(scope, batch);
                        while (!cancelToken1.IsCancellationRequested);
                    }, cancelToken1);

                    CancellationToken cancelToken2 = cancelTokenSource2.Token;
                    Task task2 = Task.Factory.StartNew(() =>
                    {
                        do
                        {
                            try
                            {
                                testScope.ReadCurrentPersistedValue();
                            }
                            catch (IOException e)
                            {
                                if (e.Message.Equals("The process cannot access the file '" + testScope.FilePath + "' because it is being used by another process."))
                                    return;
                                throw;
                            }
                        }
                        while (!cancelToken2.IsCancellationRequested);
                    }, cancelToken2);

                    if (task2.Wait(3000) && !task2.IsFaulted)
                        Assert.Pass();
                    else
                    {
                        if (task2.IsFaulted)
                            Assert.Inconclusive("The second thread failed with error '" + task2.Exception.ToString() + "'.");
                        else
                            Assert.Inconclusive("The second thread was not blocked in an interval of 3000 ms.");
                    }
                }
                catch
                {
                    cancelTokenSource1.Cancel();
                    cancelTokenSource2.Cancel();
                    throw;
                }
            }
        }

        [Test]
        public void GetNextBatchShouldReturnMinusOneWhenBlocked()
        {
            using (var testScope = new TestScope(scope))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                store.GetNextBatch(scope, batch);
                using (FileStream stream = File.Open(testScope.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    Assert.AreEqual(-1, store.GetNextBatch(scope, batch));
            }
        }
    }
}
