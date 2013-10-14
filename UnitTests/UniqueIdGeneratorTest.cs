using System;
using NSubstitute;
using NUnit.Framework;

namespace SnowMaker.UnitTests
{
    [TestFixture]
    public class UniqueIdGeneratorTest
    {
        [Test]
        public void ConstructorShouldNotRetrieveDataFromStore()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            // ReSharper disable once ObjectCreationAsStatement
            new UniqueIdGenerator(store);
            store.DidNotReceiveWithAnyArgs().GetNextBatch(null, 1);
        }

        [Test]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void MaxWriteAttemptsShouldThrowArgumentOutOfRangeExceptionWhenValueIsZero()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            // ReSharper disable once ObjectCreationAsStatement
            new UniqueIdGenerator(store)
            {
                MaxWriteAttempts = 0
            };
        }

        [Test]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void MaxWriteAttemptsShouldThrowArgumentOutOfRangeExceptionWhenValueIsNegative()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            // ReSharper disable once ObjectCreationAsStatement
            new UniqueIdGenerator(store)
            {
                MaxWriteAttempts = -1
            };
        }

        [Test]
        public void NextIdShouldReturnNumbersSequentially()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetNextBatch("test", 3).Returns(1, 251);

            var subject = new UniqueIdGenerator(store) { BatchSize = 3 };

            Assert.AreEqual(1, subject.NextId("test"));
            Assert.AreEqual(2, subject.NextId("test"));
            Assert.AreEqual(3, subject.NextId("test"));
        }

        [Test]
        public void NextIdShouldRollOverToNewBlockWhenCurrentBlockIsExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetNextBatch("test", 3).Returns(1, 251);

            var subject = new UniqueIdGenerator(store) { BatchSize = 3 };

            Assert.AreEqual(1, subject.NextId("test"));
            Assert.AreEqual(2, subject.NextId("test"));
            Assert.AreEqual(3, subject.NextId("test"));
            Assert.AreEqual(251, subject.NextId("test"));
            Assert.AreEqual(252, subject.NextId("test"));
            Assert.AreEqual(253, subject.NextId("test"));
        }

        [Test]
        public void NextIdShouldThrowExceptionWhenRetriesAreExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetNextBatch("test", 100).Returns(-1, -1, -1, 1);

            var generator = new UniqueIdGenerator(store) { MaxWriteAttempts = 3 };

            try
            {
                generator.NextId("test");
            }
            catch (Exception ex)
            {
                StringAssert.StartsWith("Failed to update the data store after 3 attempts.", ex.Message);
                return;
            }
            Assert.Fail("NextId should have thrown and been caught in the try block");
        }
    }
}
