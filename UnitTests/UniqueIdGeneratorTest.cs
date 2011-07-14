using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace Evolve.WindowsAzure.Tests
{
    [TestClass]
    public class UniqueIdGeneratorTest
    {
        [TestMethod]
        public void Test_Constructor_Uses_Store_Correctly_Under_Canonical_Load()
        {
            TestOptimisticSyncStore mock = new TestOptimisticSyncStore();
            mock.GetDataValue = "100";
            mock.TryWriteResult = true;
            Assert.AreEqual(mock.SetDataValue, null);
            UniqueIdGenerator subject = new UniqueIdGenerator(mock, 2, 2);
            // should retrieve 100, add the range to get a new upper limit of 102
            Assert.AreEqual(mock.SetDataValue, "102");
            Assert.AreEqual(101, subject.NextId());
            Assert.AreEqual(102, subject.NextId());
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void Test_Constructor_Blows_On_Corrupt_Store()
        {
            TestOptimisticSyncStore mock = new TestOptimisticSyncStore();
            mock.GetDataValue = "1oo";
            UniqueIdGenerator subject = new UniqueIdGenerator(mock);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void Test_Constructor_Blows_On_Null_Store()
        {
            TestOptimisticSyncStore mock = new TestOptimisticSyncStore();
            mock.GetDataValue = null;
            UniqueIdGenerator subject = new UniqueIdGenerator(mock);
        }

        [TestMethod]
        public void Test_NextId_Blows_On_Corrupt_Data()
        {
            TestOptimisticSyncStore mock = new TestOptimisticSyncStore();
            mock.GetDataValue = "100";
            mock.TryWriteResult = true;
            //single digit range to ensure store is used for every id
            UniqueIdGenerator subject = new UniqueIdGenerator(mock, 1, 2);
            mock.GetDataValue = "nonsense";
            subject.NextId();
            try
            {
                subject.NextId();
            }
            catch (Exception)
            {
                return;
            }

            Assert.Fail("NextId should have thrown inside try block before this Fail Assertion");
        }

        [TestMethod]
        public void Test_NextId_Deals_Numbers_Sequentially()
        {
            TestOptimisticSyncStore mock = new TestOptimisticSyncStore();
            mock.GetDataValue = "1";
            mock.TryWriteResult = true;
            UniqueIdGenerator subject = new UniqueIdGenerator(mock, 3, 0);
            mock.GetDataValue = "250";
            Assert.AreEqual(2, subject.NextId());
            Assert.AreEqual(3, subject.NextId());
            Assert.AreEqual(4, subject.NextId());
            Assert.AreEqual(251, subject.NextId());
            Assert.AreEqual(252, subject.NextId());
        }

        [TestMethod]
        public void Test_Retries_Are_Exhausted()
        {
            TestOptimisticSyncStore mock = new TestOptimisticSyncStore();
            mock.GetDataValue = "0";
            mock.TryWriteResult = false;
            try
            {
                UniqueIdGenerator subject = new UniqueIdGenerator(mock, 3, 2);
            }
            catch (Exception exc)
            {
                Assert.AreEqual(3, mock.TryWriteCount);
                Assert.AreEqual("Failed to update the OptimisticSyncStore after 3 attempts", exc.Message);
                return;
            }
            Assert.Fail("NextId should have thrown and been caught in the try block");
        }
    }

    public class TestOptimisticSyncStore : IOptimisticSyncStore
    {
        private int _tryWriteCount = 0;

        public string GetDataValue { get; set; }
        public string SetDataValue { get; private set; }
        public bool TryWriteResult { get; set; }
        public int TryWriteCount { get { return _tryWriteCount; } }

        public string GetData()
        {
            return GetDataValue; 
        }

        public bool TryOptimisticWrite(string data)
        {
            Interlocked.Increment(ref _tryWriteCount);
            SetDataValue = data;
            return TryWriteResult;
        }
    }

}
