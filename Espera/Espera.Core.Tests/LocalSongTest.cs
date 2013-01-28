using Espera.Core.Audio;
using NUnit.Framework;
using System;
using System.IO;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class LocalSongTest
    {
        [Test]
        public void HasToCache_DriveTypeIsFixed_ReturnsFalse()
        {
            var song = new LocalSong("C://", AudioType.Mp3, TimeSpan.Zero, DriveType.Fixed);

            Assert.IsFalse(song.HasToCache);
        }

        [Test]
        public void HasToCache_DriveTypeIsNetwork_ReturnsFalse()
        {
            var song = new LocalSong("C://", AudioType.Mp3, TimeSpan.Zero, DriveType.Network);

            Assert.IsFalse(song.HasToCache);
        }
    }
}