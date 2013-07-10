using Espera.Core.Audio;
using System;
using System.IO;
using Xunit;

namespace Espera.Core.Tests
{
    public class LocalSongTest
    {
        [Fact]
        public void HasToCacheShouldBeFalseIfDriveTypeIsFixed()
        {
            var song = new LocalSong("C://", AudioType.Mp3, TimeSpan.Zero, DriveType.Fixed);

            Assert.False(song.HasToCache);
        }

        [Fact]
        public void HasToCacheShouldBeFalseIfDriveTypeIsNetwork()
        {
            var song = new LocalSong("C://", AudioType.Mp3, TimeSpan.Zero, DriveType.Network);

            Assert.False(song.HasToCache);
        }
    }
}