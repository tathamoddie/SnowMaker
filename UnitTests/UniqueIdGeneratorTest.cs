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
            new UniqueIdGenerator(store);
            store.DidNotReceiveWithAnyArgs().GetData(null);
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

            var subject = new UniqueIdGenerator(store, 3, 1);

            Assert.AreEqual(1, subject.NextId("test"));
            Assert.AreEqual(2, subject.NextId("test"));
            Assert.AreEqual(3, subject.NextId("test"));
        }

        [Test]
        public void NextIdShouldRollOverToNewBlockWhenCurrentBlockIsExhausted()
        {
            var store = Substitute.For<IOptimisticDataStore>();
            store.GetData("test").Returns("0", "250");
            store.TryOptimisticWrite("test", "3").Returns(true);
            store.TryOptimisticWrite("test", "253").Returns(true);

            var subject = new UniqueIdGenerator(store, 3, 1);

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
            store.GetData("test").Returns("0");
            store.TryOptimisticWrite("test", "3").Returns(false, false, false, true);

            var generator = new UniqueIdGenerator(store, 3, 3);

            try
            {
                generator.NextId("test");
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
