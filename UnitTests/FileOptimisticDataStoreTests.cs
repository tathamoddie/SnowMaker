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
            public string ReadCurrentPersistedValue()
            {
                using (TextReader reader = new StreamReader(FilePath, FileOptimisticDataStore.Encoding))
                    return reader.ReadToEnd();
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

        [Test]
        public void ConstructorShouldNotCreateFile()
        {
            using (var testScope = new TestScope("test"))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                Assert.IsFalse(File.Exists(testScope.FilePath));
            }
        }

        [Test]
        public void ShouldCreateFileOnFirstRead()
        {
            using (var testScope = new TestScope("test"))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                store.GetData("test");
                Assert.IsTrue(File.Exists(testScope.FilePath));
                Assert.AreEqual(testScope.ReadCurrentPersistedValue(), FileOptimisticDataStore.SeedValue);
            }
        }

        [Test]
        public void ShouldNotAlterFileOnSecondRead()
        {
            using (var testScope = new TestScope("test"))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                store.GetData("test");
                DateTime lastWriteTime = File.GetLastWriteTime(testScope.FilePath);
                store.GetData("test");
                Assert.AreEqual(lastWriteTime, File.GetLastWriteTime(testScope.FilePath));
            }
        }

        [Test]
        public void GetDataShouldNotBlockFileAccess()
        {
            using (var testScope = new TestScope("test"))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                string data = store.GetData("test");

                using (FileStream stream = File.Open(testScope.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Assert.AreEqual(data, store.GetData("test"));
            }
        }

        [Test]
        public void TryOptimisticWriteShouldBlockFileAccess()
        {
            using (var testScope = new TestScope("test"))
            {
                CancellationTokenSource cancelTokenSource1 = new CancellationTokenSource();
                CancellationTokenSource cancelTokenSource2 = new CancellationTokenSource();

                try
                {
                    var store = new FileOptimisticDataStore(Path.GetTempPath());
                    store.GetData("test"); // create the file

                    CancellationToken cancelToken1 = cancelTokenSource1.Token;
                    Task task1 = Task.Factory.StartNew(() =>
                    {
                        do
                            store.TryOptimisticWrite("test", FileOptimisticDataStore.SeedValue, FileOptimisticDataStore.SeedValue);
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
        public void ShouldNotUpdateFileWhenContentsWereAltered()
        {
            using (var testScope = new TestScope("test"))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                store.GetData("test");
                Assert.IsFalse(store.TryOptimisticWrite("test", "5", "x"));
            }
        }

        [Test]
        public void TryOptimisticWriteShouldReturnFalseWhenBlocked()
        {
            using (var testScope = new TestScope("test"))
            {
                var store = new FileOptimisticDataStore(Path.GetTempPath());
                store.GetData("test");

                using (FileStream stream = File.Open(testScope.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.IsFalse(store.TryOptimisticWrite("test", FileOptimisticDataStore.SeedValue, FileOptimisticDataStore.SeedValue));
                }
            }
        }
    }
}
