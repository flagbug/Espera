using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Akavache;
using Xunit;

namespace Espera.Core.Tests
{
    public class SoundCloudSongFinderTest
    {
        public class TheGetSongsAsyncMethod
        {
            [Fact]
            public async Task NullSearchTermDefaultsToEmptyString()
            {
                var songs = new[] { new SoundCloudSong { IsStreamable = true, StreamUrl = new Uri("http://blabla.com"), PermaLinkUrl = new Uri("http://blabla") } };
                var cache = new InMemoryBlobCache();
                cache.InsertObject(BlobCacheKeys.GetKeyForSoundCloudCache(string.Empty), songs);
                var finder = new SoundCloudSongFinder(cache);

                var result = await finder.GetSongsAsync();

                Assert.Equal(1, result.Count);
            }

            [Fact]
            public async Task UsesCachedSongsIfAvailable()
            {
                const string searchTerm = "Foo";
                var songs = new[] { new SoundCloudSong { IsStreamable = true, StreamUrl = new Uri("http://blabla.com"), PermaLinkUrl = new Uri("http://blabla") } };
                var cache = new InMemoryBlobCache();
                cache.InsertObject(BlobCacheKeys.GetKeyForSoundCloudCache(searchTerm), songs);
                var finder = new SoundCloudSongFinder(cache);

                var result = await finder.GetSongsAsync(searchTerm);

                Assert.Equal(1, result.Count);
            }
        }
    }
}