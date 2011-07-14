using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace SnowMaker.UnitTests
{
    [TestFixture]
    public class DictionaryExtentionsTests
    {
        [Test]
        public void GetValueShouldReturnExistingValueWithoutUsingTheLock()
        {
            var dictionary = new Dictionary<string, string>
            {
                { "foo", "bar" }
            };

            // Act
            // null can't be used as a lock and will throw an exception if attempted
            var value = dictionary.GetValue("foo", null, null);

            // Assert
            Assert.AreEqual("bar", value);
        }

        [Test]
        public void GetValueShouldCallTheValueInitializerWithinTheLockIfTheKeyDoesntExist()
        {
            var dictionary = new Dictionary<string, string>
            {
                { "foo", "bar" }
            };

            var dictionaryLock = new object();

            // Act
            dictionary.GetValue(
                "bar",
                dictionaryLock,
                () =>
                {
                    // Assert
                    Assert.IsTrue(IsLockedOnCurrentThread(dictionaryLock));
                    return "qak";
                });
        }

        [Test]
        public void GetValueShouldStoreNewValuesAfterCallingTheValueInitializerOnce()
        {
            var dictionary = new Dictionary<string, string>
            {
                { "foo", "bar" }
            };

            var dictionaryLock = new object();

            // Arrange
            dictionary.GetValue("bar", dictionaryLock, () => "qak");

            // Act
            dictionary.GetValue(
                "bar",
                dictionaryLock,
                () =>
                {
                    // Assert
                    Assert.Fail("Value initializer should not have been called a second time.");
                    return null;
                });
        }

        static bool IsLockedOnCurrentThread(object lockObject)
        {
            var reset = new ManualResetEvent(false);
            var couldLockBeAcquiredOnOtherThread = false;
            new Thread(() =>
            {
                couldLockBeAcquiredOnOtherThread = Monitor.TryEnter(lockObject, 0);
                reset.Set();
            }).Start();
            reset.WaitOne();
            return !couldLockBeAcquiredOnOtherThread;
        }
    }
}