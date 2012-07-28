using System;
using Espera.Core.Audio;
using Espera.Core.Management;
using Moq;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class LibraryFillEventArgsTest
    {
        [Test]
        public void Constructor()
        {
            Song song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            var args = new LibraryFillEventArgs(song, 5, 10);

            Assert.AreEqual(song, args.Song);
            Assert.AreEqual(5, args.ProcessedTagCount);
            Assert.AreEqual(10, args.TotalTagCount);
        }

        [Test]
        public void Constructor_SongIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LibraryFillEventArgs(null, 0, 0));
        }

        [Test]
        public void Constructor_ProcessedTagCountIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            Song song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.Throws<ArgumentOutOfRangeException>(() => new LibraryFillEventArgs(song, -1, 0));
        }

        [Test]
        public void Constructor_TotalTagCountIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            Song song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object;

            Assert.Throws<ArgumentOutOfRangeException>(() => new LibraryFillEventArgs(song, 0, -1));
        }
    }
}