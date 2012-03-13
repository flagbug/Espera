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
        private static Playlist SetupPlaylist(IEnumerable<Song> songs)
        {
            var playlist = new Playlist();

            playlist.AddSongs(songs);

            return playlist;
        }

        private static Song[] SetupSongs(int count)
        {
            var songs = new Song[count];

            for (int i = 0; i < count; i++)
            {
                var mock = new Mock<Song>("TestPath" + count, AudioType.Mp3, TimeSpan.Zero);
                mock.Setup(p => p.LoadToCache()).Callback(() => Thread.Sleep(3000));

                songs[i] = mock.Object;
            }

            return songs;
        }

        [TestMethod]
        public void AddSongsTest()
        {
            Song[] songs = SetupSongs(4);
            Playlist playlist = SetupPlaylist(songs);

            Assert.AreEqual(4, playlist.Count());
            Assert.AreEqual(songs[0], playlist[0]);
            Assert.AreEqual(songs[1], playlist[1]);
            Assert.AreEqual(songs[2], playlist[2]);
            Assert.AreEqual(songs[3], playlist[3]);
        }

        [TestMethod]
        public void RemoveSongs_RemoveOneSong_OrderIsCorrect()
        {
            Song[] songs = SetupSongs(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.RemoveSongs(new List<int> { 1 });

            Assert.AreEqual(3, playlist.Count());
            Assert.AreEqual(songs[0], playlist[0]);
            Assert.AreEqual(songs[2], playlist[1]);
            Assert.AreEqual(songs[3], playlist[2]);
        }

        [TestMethod]
        public void CanPlayNextSong_CurrentSongIndexIsZero_ReturnsTrue()
        {
            Song[] songs = SetupSongs(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = 0;

            Assert.IsTrue(playlist.CanPlayNextSong);
        }

        [TestMethod]
        public void CanPlayNextSong_CurrentSongIndexIsPlaylistCount_ReturnsFalse()
        {
            Song[] songs = SetupSongs(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = playlist.Count();

            Assert.IsFalse(playlist.CanPlayNextSong);
        }

        [TestMethod]
        public void CanPlayNextSong_CurrentSongIndexIsNull_ReturnsFalse()
        {
            Song[] songs = SetupSongs(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = null;

            Assert.IsFalse(playlist.CanPlayNextSong);
        }

        [TestMethod]
        public void CanPlayPreviousSong_CurrentSongIndexIsZero_ReturnsFalse()
        {
            Song[] songs = SetupSongs(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = 0;

            Assert.IsFalse(playlist.CanPlayPreviousSong);
        }

        [TestMethod]
        public void CanPlayPreviousSong_CurrentSongIndexIsPlaylistCount_ReturnsTrue()
        {
            Song[] songs = SetupSongs(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = playlist.Count();

            Assert.IsTrue(playlist.CanPlayPreviousSong);
        }

        [TestMethod]
        public void CanPlayPreviousSong_CurrentSongIndexIsNull_ReturnsFalse()
        {
            Song[] songs = SetupSongs(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = null;

            Assert.IsFalse(playlist.CanPlayPreviousSong);
        }
    }
}