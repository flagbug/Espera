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
            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            Assert.IsFalse(song.Object.Equals(null));
        }

        [Test]
        public void EqualsSamePathIsTrue()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            var song2 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            Assert.IsTrue(song1.Object.Equals(song2.Object));
        }

        [Test]
        public void EqualsSameReferenceIsTrue()
        {
            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            Assert.IsTrue(song.Object.Equals(song.Object));
        }

        [Test]
        public void EqualsSongWithDifferentPathIsFalse()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            var song2 = new Mock<Song>("TestPath1", AudioType.Mp3, TimeSpan.Zero);

            Assert.IsFalse(song1.Object.Equals(song2.Object));
        }

        [Test]
        public void GetHashcode_EqualObjects_ReturnsSameHashCode()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero) { CallBase = true };
            var song2 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero) { CallBase = true };

            Assert.AreEqual(song1.GetHashCode(), song2.GetHashCode());
        }
    }
}