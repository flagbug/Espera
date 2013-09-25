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
            var song = new LocalSong("C://", TimeSpan.Zero, DriveType.Fixed, null);

            Assert.False(song.HasToCache);
        }

        [Fact]
        public void HasToCacheShouldBeFalseIfDriveTypeIsNetwork()
        {
            var song = new LocalSong("C://", TimeSpan.Zero, DriveType.Network, null);

            Assert.False(song.HasToCache);
        }
    }
}