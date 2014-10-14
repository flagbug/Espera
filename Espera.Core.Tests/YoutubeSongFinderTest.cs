using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Akavache;
using Xunit;

namespace Espera.Core.Tests
{
    public class YoutubeSongFinderTest
    {
        public class TheGetSongsAsyncMethod
        {
            [Fact]
            public async Task NullSearchTermDefaultsToEmptyString()
            {
                var songs = new[] { new YoutubeSong("http://blabla", TimeSpan.Zero) };
                BlobCache.LocalMachine.InsertObject(BlobCacheKeys.GetKeyForYoutubeCache(string.Empty), songs);

                var finder = new YoutubeSongFinder();

                var result = await finder.GetSongsAsync();

                Assert.Equal(1, result.Count);
            }

            [Fact]
            public async Task UsesCachedSongsIfAvailable()
            {
                const string searchTerm = "Foo";
                var songs = new[] { new YoutubeSong("http://blabla", TimeSpan.Zero) };
                BlobCache.LocalMachine.InsertObject(BlobCacheKeys.GetKeyForYoutubeCache(searchTerm), songs);

                var finder = new YoutubeSongFinder();

                var result = await finder.GetSongsAsync(searchTerm);

                Assert.Equal(1, result.Count);
            }
        }
    }
}