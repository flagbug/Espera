using System;
using Espera.Core.Audio;
using Moq;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public sealed class SongTest
    {
        [Test]
        public void ClearCache_SongHasNotToBeCached_ThrowsInvalidOperationException()
        {
            var song = new Mock<Song>("TestPath1", AudioType.Mp3, TimeSpan.Zero);
            song.SetupGet(p => p.HasToCache).Returns(false);

            Assert.Throws<InvalidOperationException>(() => song.Object.ClearCache());
        }

        [Test]
        public void EqualsNullIsFalse()
        {
            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.IsFalse(song.Equals(null));
        }

        [Test]
        public void EqualsSamePathIsTrue()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;
            var song2 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.IsTrue(song1.Equals(song2));
        }

        [Test]
        public void EqualsSameReferenceIsTrue()
        {
            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.IsTrue(song.Equals(song));
        }

        [Test]
        public void EqualsSongWithDifferentPathIsFalse()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;
            var song2 = new Mock<Song>("TestPath1", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.IsFalse(song1.Equals(song2));
        }

        [Test]
        public void GetHashcode_EqualObjects_ReturnsEqualHashCodes()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero) { CallBase = true }.Object;
            var song2 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero) { CallBase = true }.Object;

            Assert.AreEqual(song1.GetHashCode(), song2.GetHashCode());
        }
    }
}