using System;
using System.Collections.Generic;
using System.Linq;
using Espera.Core.Audio;
using Espera.Core.Management;
using Moq;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public sealed class PlaylistTest
    {
        [Test]
        public void AddSongs_PlaylistContainsSongs()
        {
            Song[] songs = SetupSongMocks(4);
            Playlist playlist = SetupPlaylist(songs);

            Assert.AreEqual(4, playlist.Count());
            Assert.AreEqual(songs[0], playlist[0]);
            Assert.AreEqual(songs[1], playlist[1]);
            Assert.AreEqual(songs[2], playlist[2]);
            Assert.AreEqual(songs[3], playlist[3]);
        }

        [Test]
        public void AddSongs_ArgumentIsNull_ThrowsArgumentNullException()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentNullException>(() => playlist.AddSongs(null));
        }

        [Test]
        public void CanPlayNextSong_CurrentSongIndexIsNull_ReturnsFalse()
        {
            Song[] songs = SetupSongMocks(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = null;

            Assert.IsFalse(playlist.CanPlayNextSong);
        }

        [Test]
        public void CanPlayNextSong_CurrentSongIndexIsPlaylistCount_ReturnsFalse()
        {
            Song[] songs = SetupSongMocks(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = playlist.Count();

            Assert.IsFalse(playlist.CanPlayNextSong);
        }

        [Test]
        public void CanPlayNextSong_CurrentSongIndexIsZero_ReturnsTrue()
        {
            Song[] songs = SetupSongMocks(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = 0;

            Assert.IsTrue(playlist.CanPlayNextSong);
        }

        [Test]
        public void CanPlayPreviousSong_CurrentSongIndexIsNull_ReturnsFalse()
        {
            Song[] songs = SetupSongMocks(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = null;

            Assert.IsFalse(playlist.CanPlayPreviousSong);
        }

        [Test]
        public void CanPlayPreviousSong_CurrentSongIndexIsPlaylistCount_ReturnsTrue()
        {
            Song[] songs = SetupSongMocks(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = playlist.Count();

            Assert.IsTrue(playlist.CanPlayPreviousSong);
        }

        [Test]
        public void CanPlayPreviousSong_CurrentSongIndexIsZero_ReturnsFalse()
        {
            Song[] songs = SetupSongMocks(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = 0;

            Assert.IsFalse(playlist.CanPlayPreviousSong);
        }

        [Test]
        public void GetIndexes_OneSong_ReturnsCorrectIndexes()
        {
            Song song = SetupSongMock("Song", true);

            Playlist playlist = SetupPlaylist(song);

            int index = playlist.GetIndexes(new[] { song }).Single();

            Assert.AreEqual(0, index);
        }

        [Test]
        public void GetIndexes_OneSongWithMultipleReferences_ReturnsCorrectIndexes()
        {
            Song song = SetupSongMock("Song", true);

            Playlist playlist = SetupPlaylist(Enumerable.Repeat(song, 3));

            IEnumerable<int> indexes = playlist.GetIndexes(new[] { song });

            Assert.IsTrue(indexes.SequenceEqual(new[] { 0, 1, 2 }));
        }

        [Test]
        public void GetIndexes_MultipleSongs_ReturnsCorrectIndexes()
        {
            Song[] songs = SetupSongMocks(3, true);

            Playlist playlist = SetupPlaylist(songs);

            IEnumerable<int> indexes = playlist.GetIndexes(songs);

            Assert.IsTrue(indexes.SequenceEqual(new[] { 0, 1, 2 }));
        }

        [Test]
        public void GetIndexes_PassSongsThatAreNotInPlaylist_ReturnsNoIndexes()
        {
            Song[] songs = SetupSongMocks(4, true);

            Playlist playlist = SetupPlaylist(songs.Take(2));

            IEnumerable<int> indexes = playlist.GetIndexes(songs.Skip(2));

            Assert.IsEmpty(indexes);
        }

        [Test]
        public void InsertMove_FromIndexIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.InsertMove(-1, 0));
        }

        [Test]
        public void InsertMove_ToIndexIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.InsertMove(0, -1));
        }

        [Test]
        public void InsertMove_ToIndexIsEqualFromIndex_ThrowsArgumentException()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentException>(() => playlist.InsertMove(0, 0));
        }

        [Test]
        public void InsertMove_ToIndexIsBiggerThanFromIndex_ThrowsArgumentException()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentException>(() => playlist.InsertMove(0, 1));
        }

        [Test]
        public void InsertMove_InsertSongToPlaylist_OrderIsCorrent()
        {
            Song[] songs = SetupSongMocks(5);

            Playlist playlist = SetupPlaylist(songs);

            playlist.InsertMove(3, 1);

            Assert.AreEqual(songs[0], playlist[0]);
            Assert.AreEqual(songs[3], playlist[1]);
            Assert.AreEqual(songs[1], playlist[2]);
            Assert.AreEqual(songs[2], playlist[3]);
        }

        [Test]
        public void RemoveSongs_RemoveMultipleSongs_OrderIsCorrect()
        {
            Song[] songs = SetupSongMocks(7);
            Playlist playlist = SetupPlaylist(songs);

            playlist.RemoveSongs(new[] { 1, 3, 4 });

            Assert.AreEqual(4, playlist.Count());
            Assert.AreEqual(songs[0], playlist[0]);
            Assert.AreEqual(songs[2], playlist[1]);
            Assert.AreEqual(songs[5], playlist[2]);
            Assert.AreEqual(songs[6], playlist[3]);
        }

        [Test]
        public void RemoveSongs_ArgumentIsNull_ThrowsArgumentNullException()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentNullException>(() => playlist.RemoveSongs(null));
        }

        [Test]
        public void RemoveSongs_RemoveOneSong_OrderIsCorrect()
        {
            Song[] songs = SetupSongMocks(4);
            Playlist playlist = SetupPlaylist(songs);

            playlist.RemoveSongs(new[] { 1 });

            Assert.AreEqual(3, playlist.Count());
            Assert.AreEqual(songs[0], playlist[0]);
            Assert.AreEqual(songs[2], playlist[1]);
            Assert.AreEqual(songs[3], playlist[2]);
        }

        [Test]
        public void RemoveSongsCorrectsCurrentSongIndex()
        {
            Song[] songs = SetupSongMocks(2);

            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = 1;

            playlist.RemoveSongs(new[] { 0 });

            Assert.AreEqual(0, playlist.CurrentSongIndex);
        }

        [Test]
        public void ShuffleMigratesCurrentSongIndex()
        {
            Song[] songs = SetupSongMocks(100, true);

            Playlist playlist = SetupPlaylist(songs);

            playlist.CurrentSongIndex = 0;

            playlist.Shuffle();

            int newIndex = playlist.GetIndexes(new[] { songs[0] }).First();

            Assert.AreEqual(newIndex, playlist.CurrentSongIndex);
        }

        private static Playlist SetupPlaylist(Song song)
        {
            return SetupPlaylist(new[] { song });
        }

        private static Playlist SetupPlaylist(IEnumerable<Song> songs)
        {
            var playlist = new Playlist("Test Playlist");

            playlist.AddSongs(songs);

            return playlist;
        }

        private static Song SetupSongMock(string name = "Song", bool callBase = false, AudioType audioType = AudioType.Mp3, TimeSpan? duration = null)
        {
            if (duration == null)
            {
                duration = TimeSpan.Zero;
            }

            return new Mock<Song>(name, audioType, duration) { CallBase = callBase }.Object;
        }

        private static Song[] SetupSongMocks(int count, bool callBase = false)
        {
            var songs = new Song[count];

            for (int i = 0; i < count; i++)
            {
                songs[i] = SetupSongMock("Song" + i, callBase);
            }

            return songs;
        }
    }
}