using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Akavache;
using NSubstitute;
using Xunit;

namespace Espera.Core.Tests
{
    public class ArtworkCacheTest
    {
        public class TheRetrieveMethod
        {
            [Fact]
            public async Task ThrowsArgumentNullExceptionIfKeyIsNull()
            {
                var blobCache = new TestBlobCache();
                var artworkCache = new ArtworkCache(blobCache);

                await Helpers.ThrowsAsync<ArgumentNullException>(() => artworkCache.Retrieve(null, 100, 100));
            }
        }

        public class TheStoreMethod
        {
            [Fact]
            public async Task DoesntStoreArtworkIfAlreadyInLocalCache()
            {
                var blobCache = Substitute.For<IBlobCache>();
                var artworkCache = new ArtworkCache(blobCache);

                var data = new byte[] { 0, 1 };

                await artworkCache.Store(data);
                await artworkCache.Store(data);

                blobCache.Received(1).Insert(Arg.Any<string>(), Arg.Any<byte[]>());
            }

            [Fact]
            public async Task NullDataThrowsArgumentNullException()
            {
                var blobCache = new TestBlobCache();
                var artworkCache = new ArtworkCache(blobCache);

                await Helpers.ThrowsAsync<ArgumentNullException>(() => artworkCache.Store(null));
            }

            [Fact]
            public async Task StoresArtworkInBlobCache()
            {
                var blobCache = new TestBlobCache();
                var artworkCache = new ArtworkCache(blobCache);

                var data = new byte[] { 0, 1 };

                string key = await artworkCache.Store(data);

                Assert.Equal(data, await blobCache.GetAsync(key));
            }
        }
    }
}