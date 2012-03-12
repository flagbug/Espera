using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Espera.Core.Audio;
using Espera.Core.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Espera.Core.Tests
{
    [TestClass]
    public class PlaylistTest
    {
        private readonly Mock<Song> song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

        private readonly Mock<Song> song1 = new Mock<Song>("TestPath1", AudioType.Mp3, TimeSpan.Zero);

        private readonly Mock<Song> song2 = new Mock<Song>("TestPath2", AudioType.Mp3, TimeSpan.Zero);

        private readonly Mock<Song> song3 = new Mock<Song>("TestPath3", AudioType.Mp3, TimeSpan.Zero);

        public PlaylistTest()
        {
            this.song.Setup(p => p.LoadToCache()).Callback(() => Thread.Sleep(1000));

            this.song1.Setup(p => p.LoadToCache()).Callback(() => Thread.Sleep(2000));

            this.song2.Setup(p => p.LoadToCache()).Callback(() => Thread.Sleep(3000));

            this.song3.Setup(p => p.LoadToCache()).Callback(() => Thread.Sleep(4000));
        }

        private Playlist SetupPlaylist()
        {
            var playlist = new Playlist();

            playlist.AddSongs(new List<Song> { this.song.Object, this.song1.Object, this.song2.Object, this.song3.Object });

            return playlist;
        }

        [TestMethod]
        public void AddSongsTest()
        {
            Playlist playlist = this.SetupPlaylist();

            Assert.AreEqual(4, playlist.Count());
            Assert.AreEqual(this.song.Object, playlist[0]);
            Assert.AreEqual(this.song1.Object, playlist[1]);
            Assert.AreEqual(this.song2.Object, playlist[2]);
            Assert.AreEqual(this.song3.Object, playlist[3]);
        }

        public void RemoveSongsTest()
        {
            Playlist playlist = this.SetupPlaylist();

            playlist.RemoveSongs(new List<int> { 1 });

            Assert.AreEqual(3, playlist.Count());
            Assert.AreEqual(this.song.Object, playlist[0]);
            Assert.AreEqual(this.song2.Object, playlist[1]);
            Assert.AreEqual(this.song3.Object, playlist[1]);
        }
    }
}