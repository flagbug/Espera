using System;
using Espera.Core.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Espera.Core.Tests
{
    [TestClass]
    public class SongTest
    {
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ClearCache_SongHasNotToBeCached_ThrowsInvalidOperationException()
        {
            var song = new Mock<Song>("TestPath1", AudioType.Mp3, TimeSpan.Zero);
            song.SetupGet(p => p.HasToCache).Returns(false);

            song.Object.ClearCache();
        }

        [TestMethod]
        public void EqualsNullIsFalse()
        {
            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            Assert.IsFalse(song.Object.Equals(null));
        }

        [TestMethod]
        public void EqualsSamePathIsTrue()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            var song2 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            Assert.IsTrue(song1.Object.Equals(song2.Object));
        }

        [TestMethod]
        public void EqualsSameReferenceIsTrue()
        {
            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            Assert.IsTrue(song.Object.Equals(song.Object));
        }

        [TestMethod]
        public void EqualsSongWithDifferentPathIsFalse()
        {
            var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            var song2 = new Mock<Song>("TestPath1", AudioType.Mp3, TimeSpan.Zero);

            Assert.IsFalse(song1.Object.Equals(song2.Object));
        }
    }
}