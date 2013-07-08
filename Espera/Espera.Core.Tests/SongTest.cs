using Espera.Core.Audio;
using Moq;
using System;
using Xunit;

namespace Espera.Core.Tests
{
    public sealed class SongTest
    {
        [Fact]
        public void ClearCacheThrowsInvalidOperationExceptionIfSongHasNotToBeCached()
        {
            var song = new Mock<Song>("TestPath1", AudioType.Mp3, TimeSpan.Zero);
            song.SetupGet(p => p.HasToCache).Returns(false);

            Assert.Throws<InvalidOperationException>(() => song.Object.ClearCache());
        }

        [Fact]
        public void EqualsNullIsFalse()
        {
            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.False(song.Equals(null));
        }

        [Fact]
        public void EqualsSamePathIsTrue()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;
            var song2 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.True(song1.Equals(song2));
        }

        [Fact]
        public void EqualsSameReferenceIsTrue()
        {
            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.True(song.Equals(song));
        }

        [Fact]
        public void EqualsSongWithDifferentPathIsFalse()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;
            var song2 = new Mock<Song>("TestPath1", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.False(song1.Equals(song2));
        }

        [Fact]
        public void GetHashcodeReturnsEqualHashCodesForEqualObjects()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero) { CallBase = true }.Object;
            var song2 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero) { CallBase = true }.Object;

            Assert.Equal(song1.GetHashCode(), song2.GetHashCode());
        }
    }
}