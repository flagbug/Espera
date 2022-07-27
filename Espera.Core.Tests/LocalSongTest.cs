﻿using System;

namespace Espera.Core.Tests
{
    public class LocalSongTest
    {
        public class TheConstructor
        {
            [Fact]
            public void SetsArtworkKey()
            {
                var key = BlobCacheKeys.GetKeyForArtwork(new byte[] { 0, 1 });

                var song = new LocalSong("C://Bla", TimeSpan.Zero, key);

                Assert.Equal(key, song.ArtworkKey);
            }
        }
    }
}