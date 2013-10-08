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
            store.DidNotReceiveWithAnyArgs().GetData(null);
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
        [ExpectedException(typeof(UniqueIdGenerationException))]
        public void NextIdShouldThrowExceptionOnCorruptData()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("abc");

            var generator = new UniqueIdGenerator(store);

            generator.NextId("test");
        }

        [Test]
        [ExpectedException(typeof(UniqueIdGenerationException))]
        public void NextIdShouldThrowExceptionOnNullData()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns((string)null);

            var generator = new UniqueIdGenerator(store);

            generator.NextId("test");
        }

        [Test]
        public void NextIdShouldReturnNumbersSequentially()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("0", "250");
            store.TryOptimisticWrite("test", "3").Returns(true);

            var subject = new UniqueIdGenerator(store)
            {
                BatchSize = 3
            };

            Assert.AreEqual(0, subject.NextId("test"));
            Assert.AreEqual(1, subject.NextId("test"));
            Assert.AreEqual(2, subject.NextId("test"));
        }

        [Test]
        public void NextIdShouldRollOverToNewBlockWhenCurrentBlockIsExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("0", "250");
            store.TryOptimisticWrite("test", "3").Returns(true);
            store.TryOptimisticWrite("test", "253").Returns(true);

            var subject = new UniqueIdGenerator(store)
            {
                BatchSize = 3
            };

            Assert.AreEqual(0, subject.NextId("test"));
            Assert.AreEqual(1, subject.NextId("test"));
            Assert.AreEqual(2, subject.NextId("test"));
            Assert.AreEqual(250, subject.NextId("test"));
            Assert.AreEqual(251, subject.NextId("test"));
            Assert.AreEqual(252, subject.NextId("test"));
        }

        [Test]
        public void NextIdShouldThrowExceptionWhenRetriesAreExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("0");
            store.TryOptimisticWrite("test", "3").Returns(false, false, false, true);

            var generator = new UniqueIdGenerator(store)
            {
                MaxWriteAttempts = 3
            };

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
