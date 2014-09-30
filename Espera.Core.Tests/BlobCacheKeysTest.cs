using Xunit;

namespace Espera.Core.Tests
{
    public class BlobCacheKeysTest
    {
        public class TheGetArtworkKeyWithSizeMethod
        {
            [Fact]
            public void SmokeTest()
            {
                const string artworkKey = BlobCacheKeys.Artwork + "mycoolhash";

                string keyWithSize = BlobCacheKeys.GetArtworkKeyWithSize(artworkKey, 50);

                Assert.Equal(artworkKey + "-50x50", keyWithSize);
            }
        }
    }
}