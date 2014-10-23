using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Akavache;
using NSubstitute;
using Xunit;

namespace Espera.Core.Tests
{
    public class ArtworkCacheTest
    {
        public class TheFetchOnlineMethod
        {
            [Fact]
            public async Task FailedRequestThrowsException()
            {
                var fetcher = Substitute.For<IArtworkFetcher>();
                fetcher.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Observable.Throw<Uri>(new ArtworkFetchException(String.Empty, null)).ToTask());
                var blobCache = new InMemoryBlobCache();
                var fixture = new ArtworkCache(blobCache, fetcher);

                await Helpers.ThrowsAsync<ArtworkCacheException>(() => fixture.FetchOnline("A", "B"));
            }

            [Fact]
            public async Task NotFoundRequestIsMarked()
            {
                var fetcher = Substitute.For<IArtworkFetcher>();
                fetcher.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult<Uri>(null));
                var blobCache = new InMemoryBlobCache();
                var fixture = new ArtworkCache(blobCache, fetcher);
                string artist = "A";
                string album = "B";
                string lookupKey = BlobCacheKeys.GetKeyForOnlineArtwork(artist, album);

                await fixture.FetchOnline(artist, album);

                Assert.Equal("FAILED", await blobCache.GetObject<string>(lookupKey));
            }

            [Fact]
            public async Task NotFoundRequestReturnsNull()
            {
                var fetcher = Substitute.For<IArtworkFetcher>();
                fetcher.RetrieveAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult<Uri>(null));
                var blobCache = new InMemoryBlobCache();
                var fixture = new ArtworkCache(blobCache, fetcher);

                string returned = await fixture.FetchOnline("A", "B");

                Assert.Null(returned);
            }

            [Fact]
            public async Task PullsSearchesFromCache()
            {
                string artist = "A";
                string album = "B";
                string key = BlobCacheKeys.GetKeyForOnlineArtwork(artist, album);
                var fetcher = Substitute.For<IArtworkFetcher>();
                var blobCache = new InMemoryBlobCache();
                await blobCache.InsertObject(key, "TestArtworkKey");
                var fixture = new ArtworkCache(blobCache, fetcher);

                string returned = await fixture.FetchOnline(artist, album);

                Assert.Equal("TestArtworkKey", returned);
                fetcher.DidNotReceiveWithAnyArgs().RetrieveAsync(null, null);
            }
        }

        public class TheRetrieveMethod
        {
            [Fact]
            public async Task ThrowsArgumentNullExceptionIfKeyIsNull()
            {
                var blobCache = new InMemoryBlobCache();
                var artworkCache = new ArtworkCache(blobCache);

                await Helpers.ThrowsAsync<ArgumentNullException>(() => artworkCache.Retrieve(null, 100, 100));
            }

            [Fact]
            public async Task ThrowsArgumentOutOfRangeExceptionIfSizeIsLessThanZero()
            {
                var blobCache = new InMemoryBlobCache();
                var artworkCache = new ArtworkCache(blobCache);

                await Helpers.ThrowsAsync<ArgumentOutOfRangeException>(() => artworkCache.Retrieve("somekey", -1, 0));
                await Helpers.ThrowsAsync<ArgumentOutOfRangeException>(() => artworkCache.Retrieve("somekey", 0, -1));
            }
        }

        public class TheStoreMethod
        {
            [Fact]
            public void CanStoreMulipleArtworksIfArtworksExistsInCache()
            {
                var blobCache = new InMemoryBlobCache();
                var data = new byte[] { 0, 1 };
                string key = BlobCacheKeys.GetKeyForArtwork(data);
                blobCache.Insert(key, data);
                var fixture = new ArtworkCache(blobCache);

                Task firstTask = fixture.Store(key, data);
                Task secondTask = fixture.Store(key, data);
                Task thrirdTask = fixture.Store(key, data);

                Assert.True(firstTask.IsCompleted);
                Assert.True(secondTask.IsCompleted);
                Assert.True(thrirdTask.IsCompleted);
            }

            [Fact]
            public async Task DoesntStoreArtworkIfAlreadyInLocalCache()
            {
                var blobCache = Substitute.For<IBlobCache>();
                var artworkCache = new ArtworkCache(blobCache);
                var data = new byte[] { 0, 1 };
                string key = BlobCacheKeys.GetKeyForArtwork(data);

                await artworkCache.Store(key, data);

                blobCache.GetCreatedAt(Arg.Any<string>()).Returns(Observable.Return(new DateTimeOffset?(DateTimeOffset.MaxValue)));

                await artworkCache.Store(key, data);

                blobCache.Received(1).Insert(Arg.Any<string>(), Arg.Any<byte[]>());
            }

            [Fact]
            public async Task NullDataThrowsArgumentNullException()
            {
                var blobCache = new InMemoryBlobCache();
                var artworkCache = new ArtworkCache(blobCache);

                await Helpers.ThrowsAsync<ArgumentNullException>(() => artworkCache.Store("yaddakey", null));
            }

            [Fact]
            public void SameKeysWaitOnFirstToFinish()
            {
                var signal = new AsyncSubject<Unit>();
                var blobCache = Substitute.For<IBlobCache>();
                blobCache.Insert(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DateTimeOffset?>()).Returns(signal);
                var fixture = new ArtworkCache(blobCache);
                var data = new byte[] { 0, 1 };
                string key = BlobCacheKeys.GetKeyForArtwork(data);

                Task firstTask = fixture.Store(key, data);

                Task secondTask = fixture.Store(key, data);

                Assert.False(firstTask.IsCompleted);
                Assert.False(secondTask.IsCompleted);

                signal.OnNext(Unit.Default);
                signal.OnCompleted();

                Assert.True(firstTask.IsCompleted);
                Assert.True(secondTask.IsCompleted);
            }

            [Fact]
            public async Task StoresArtworkInBlobCache()
            {
                var blobCache = new InMemoryBlobCache();
                var artworkCache = new ArtworkCache(blobCache);

                var data = new byte[] { 0, 1 };
                string key = BlobCacheKeys.GetKeyForArtwork(data);

                await artworkCache.Store(key, data);

                Assert.Equal(data, await blobCache.Get(key));
            }
        }
    }
}