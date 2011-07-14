using System;
using NUnit.Framework;

namespace SnowMaker.UnitTests
{
    [TestFixture]
    public class UniqueIdGeneratorTest
    {
        [Test]
        public void ConstructorShouldRetrieveNewIdBlockFromStore()
        {
            var mock = new TestOptimisticSyncStore {GetDataValue = "100", TryWriteResult = true};
            Assert.AreEqual(mock.SetDataValue, null);

            var subject = new UniqueIdGenerator(mock, 2, 2);

            // should retrieve 100, add the range to get a new upper limit of 102
            Assert.AreEqual(mock.SetDataValue, "102");
            Assert.AreEqual(101, subject.NextId());
            Assert.AreEqual(102, subject.NextId());
        }

        [Test]
        [ExpectedException(typeof(Exception))]
        public void ConstructorShouldThrowExceptionOnCorruptStore()
        {
            var mock = new TestOptimisticSyncStore {GetDataValue = "1oo"};
            new UniqueIdGenerator(mock);
        }

        [Test]
        [ExpectedException(typeof(Exception))]
        public void ConstructorShouldThrowExceptionOnNullStore()
        {
            var mock = new TestOptimisticSyncStore {GetDataValue = null};
            new UniqueIdGenerator(mock);
        }

        [Test]
        public void NextIdShouldThrowExceptionOnCorruptData()
        {
            var mock = new TestOptimisticSyncStore {GetDataValue = "100", TryWriteResult = true};
            //single digit range to ensure store is used for every id
            var subject = new UniqueIdGenerator(mock, 1, 2);
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

        [Test]
        public void NextIdShouldReturnNumbersSequentially()
        {
            var mock = new TestOptimisticSyncStore {GetDataValue = "1", TryWriteResult = true};
            var subject = new UniqueIdGenerator(mock, 3, 0);
            mock.GetDataValue = "250";
            Assert.AreEqual(2, subject.NextId());
            Assert.AreEqual(3, subject.NextId());
            Assert.AreEqual(4, subject.NextId());
            Assert.AreEqual(251, subject.NextId());
            Assert.AreEqual(252, subject.NextId());
        }

        [Test]
        public void ConstructorShouldThrowExceptionWhenRetriesAreExhausted()
        {
            var mock = new TestOptimisticSyncStore {GetDataValue = "0", TryWriteResult = false};
            try
            {
                new UniqueIdGenerator(mock, 3, 2);
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
}
