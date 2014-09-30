using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public class KeyedMemoizingSemaphoreTest
    {
        public class TheReleaseMethod
        {
            [Fact]
            public void NotAwaitedKeyThrowsKeyNotFoundException()
            {
                var semaphore = new KeyedMemoizingSemaphore();

                Assert.Throws<KeyNotFoundException>(() => semaphore.Release("wat"));
            }
        }

        public class TheWaitMethod
        {
            [Fact]
            public async Task DifferentKeysIgnoreEachOther()
            {
                var semaphore = new KeyedMemoizingSemaphore();

                await semaphore.Wait("key1");
                await semaphore.Wait("key2");

                Task awaiter1 = semaphore.Wait("key1");
                Task awaiter2 = semaphore.Wait("key2");

                Assert.False(awaiter1.IsCompleted);
                Assert.False(awaiter2.IsCompleted);

                semaphore.Release("key1");

                Assert.True(awaiter1.IsCompleted);
                Assert.False(awaiter2.IsCompleted);

                semaphore.Release("key2");

                Assert.True(awaiter2.IsCompleted);
            }

            [Fact]
            public async Task FirstWaitIsReleasedImmediately()
            {
                var semaphore = new KeyedMemoizingSemaphore();

                await semaphore.Wait("key");
            }

            [Fact]
            public async Task SameKeysWaitOnRelease()
            {
                var semaphore = new KeyedMemoizingSemaphore();

                await semaphore.Wait("key");

                Task awaiter = semaphore.Wait("key");

                Assert.False(awaiter.IsCompleted);

                semaphore.Release("key");

                Assert.True(awaiter.IsCompleted);
            }
        }
    }
}