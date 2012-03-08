using System;
using Espera.Core.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Espera.Core.Tests
{
    [TestClass]
    public class SongTest
    {
        private readonly Song song;
        private readonly Song samePath;
        private readonly Song differentPath;

        public SongTest()
        {
            this.song = new LocalSong("C://", AudioType.Mp3, TimeSpan.Zero);
            this.samePath = new LocalSong("C://", AudioType.Mp3, TimeSpan.Zero);
            this.differentPath = new LocalSong("D://", AudioType.Mp3, TimeSpan.Zero);
        }

        [TestMethod]
        public void EqualsNullIsFalse()
        {
            Assert.IsFalse(this.song.Equals(null));
        }

        [TestMethod]
        public void EqualsSongWithDifferentPathIsFalse()
        {
            Assert.IsFalse(this.song.Equals(this.differentPath));
        }

        [TestMethod]
        public void EqualsSameReferenceIsTrue()
        {
            Assert.IsTrue(this.song.Equals(this.song));
        }

        [TestMethod]
        public void EqualsSamePathIsTrue()
        {
            Assert.IsTrue(this.song.Equals(this.samePath));
        }
    }
}