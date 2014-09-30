using System;
using Xunit;

namespace Espera.Core.Tests
{
    public class SoundCloudSongTest
    {
        public class ThePlaybackPathProperty
        {
            [Fact]
            public void PrioritizesDeserialization()
            {
                var song = new SoundCloudSong("http://soundcloud.com", "http://streamable.com");

                Assert.Equal("http://streamable.com", song.PlaybackPath);
            }

            [Fact]
            public void PrioritizesDownloadStream()
            {
                var song = new SoundCloudSong
                {
                    IsStreamable = true,
                    StreamUrl = new Uri("http://streamable.com"),
                    IsDownloadable = true,
                    DownloadUrl = new Uri("http://downloadable.com")
                };

                Assert.Equal(song.PlaybackPath, song.DownloadUrl.ToString());
            }
        }
    }
}
