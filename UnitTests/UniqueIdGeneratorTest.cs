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
            new UniqueIdGenerator(store, "test");
            store.DidNotReceive().GetData("test");
        }

        [Test]
        [ExpectedException(typeof(Exception))]
        public void NextIdShouldThrowExceptionOnCorruptData()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("abc");

            var generator = new UniqueIdGenerator(store, "test");

            generator.NextId();
        }

        [Test]
        [ExpectedException(typeof(Exception))]
        public void NextIdShouldThrowExceptionOnNullData()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns((string)null);

            var generator = new UniqueIdGenerator(store, "test");

            generator.NextId();
        }

        [Test]
        public void NextIdShouldReturnNumbersSequentially()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("0", "250");
            store.TryOptimisticWrite("test", "3").Returns(true);

            var subject = new UniqueIdGenerator(store, "test", 3, 0);

            Assert.AreEqual(1, subject.NextId());
            Assert.AreEqual(2, subject.NextId());
            Assert.AreEqual(3, subject.NextId());
        }

        [Test]
        public void NextIdShouldRollOverToNewBlockWhenCurrentBlockIsExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("0", "250");
            store.TryOptimisticWrite("test", "3").Returns(true);
            store.TryOptimisticWrite("test", "253").Returns(true);

            var subject = new UniqueIdGenerator(store, "test", 3, 0);

            Assert.AreEqual(1, subject.NextId());
            Assert.AreEqual(2, subject.NextId());
            Assert.AreEqual(3, subject.NextId());
            Assert.AreEqual(251, subject.NextId());
            Assert.AreEqual(252, subject.NextId());
            Assert.AreEqual(253, subject.NextId());
        }

        [Test]
        public void NextIdShouldThrowExceptionWhenRetriesAreExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("0");
            store.TryOptimisticWrite("test", "3").Returns(false, false, false, true);

            var generator = new UniqueIdGenerator(store, "test", 3, 2);

            try
            {
                generator.NextId();
            }
            catch (Exception exc)
            {
                Assert.AreEqual("Failed to update the OptimisticSyncStore after 3 attempts", exc.Message);
                return;
            }
            Assert.Fail("NextId should have thrown and been caught in the try block");
        }
    }
}
