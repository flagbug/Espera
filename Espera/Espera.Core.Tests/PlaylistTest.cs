using Espera.Core.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public sealed class PlaylistTest
    {
        [Fact]
        public void AddSongsSetsCorrectPlaylistIndexes()
        {
            Song[] songs = Helpers.SetupSongMocks(2);

            var playlist = new Playlist("Playlist");

            playlist.AddSongs(songs);

            Assert.Equal(0, playlist[0].Index);
            Assert.Equal(1, playlist[1].Index);
        }

        [Fact]
        public void AddSongsShouldThrowArgumentNullExceptionIfArgumentIsNull()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentNullException>(() => playlist.AddSongs(null));
        }

        [Fact]
        public void AddSongsSmokeTest()
        {
            Song[] songs = Helpers.SetupSongMocks(4);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            Assert.Equal(4, playlist.Count());
            Assert.Equal(songs[0], playlist[0].Song);
            Assert.Equal(songs[1], playlist[1].Song);
            Assert.Equal(songs[2], playlist[2].Song);
            Assert.Equal(songs[3], playlist[3].Song);
        }

        [Fact]
        public async Task CanPlayNextSongReturnsFalseIfCurrentSongIndexIsLastSong()
        {
            Song[] songs = Helpers.SetupSongMocks(4);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.CurrentSongIndex.Value = 3;

            Assert.False(await playlist.CanPlayNextSong.FirstAsync());
        }

        [Fact]
        public async Task CanPlayNextSongReturnsFalseIfCurrentSongIndexIsNull()
        {
            Song[] songs = Helpers.SetupSongMocks(4);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.CurrentSongIndex.Value = null;

            Assert.False(await playlist.CanPlayNextSong.FirstAsync());
        }

        [Fact]
        public async Task CanPlayNextSongReturnsFalseIfPlaylistIsEmpty()
        {
            var playlist = new Playlist("Playlist");

            Assert.False(await playlist.CanPlayNextSong.FirstAsync());
        }

        [Fact]
        public async Task CanPlayNextSongReturnsTrueIfCurrentSongIndexIsZero()
        {
            Song[] songs = Helpers.SetupSongMocks(4);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.CurrentSongIndex.Value = 0;

            Assert.True(await playlist.CanPlayNextSong.FirstAsync());
        }

        [Fact]
        public async Task CanPlayPreviousSongReturnsFalseIfCurrentSongIndexIsNull()
        {
            Song[] songs = Helpers.SetupSongMocks(4);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.CurrentSongIndex.Value = null;

            Assert.False(await playlist.CanPlayPreviousSong.FirstAsync());
        }

        [Fact]
        public async Task CanPlayPreviousSongReturnsFalseIfCurrentSongIndexIsZero()
        {
            Song[] songs = Helpers.SetupSongMocks(4);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.CurrentSongIndex.Value = 0;

            Assert.False(await playlist.CanPlayPreviousSong.FirstAsync());
        }

        [Fact]
        public async Task CanPlayPreviousSongReturnsFalseIfPlaylistIsEmpty()
        {
            var playlist = new Playlist("Playlist");

            Assert.False(await playlist.CanPlayPreviousSong.FirstAsync());
        }

        [Fact]
        public async Task CanPlayPreviousSongReturnsTrueIfCurrentSongIndexIsLastSong()
        {
            Song[] songs = Helpers.SetupSongMocks(4);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.CurrentSongIndex.Value = 3;

            Assert.True(await playlist.CanPlayPreviousSong.FirstAsync());
        }

        [Fact]
        public void CurrentSongIndexSetterSmokeTest()
        {
            new Playlist("Playlist").CurrentSongIndex.Value = null;
        }

        [Fact]
        public void CurrentSongIndexSetterThrowsArgumentOutOfRangeExceptionIfSetToZeroWhilePlaylistIsEmpty()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.CurrentSongIndex.Value = 0);
        }

        [Fact]
        public void CurrentSongIndexSetterThrowsArgumentOutOfRangeExceptionIfValueIsNotInPlaylistRange()
        {
            Song[] songs = Helpers.SetupSongMocks(3);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.CurrentSongIndex.Value = 3);
        }

        [Fact]
        public void GetIndexesReturnsCorrectIndexesForMultipleSongs()
        {
            Song[] songs = Helpers.SetupSongMocks(3, true);

            Playlist playlist = Helpers.SetupPlaylist(songs);

            IEnumerable<int> indexes = playlist.GetIndexes(songs);

            Assert.Equal(new[] { 0, 1, 2 }, indexes);
        }

        [Fact]
        public void GetIndexesReturnsCorrectIndexesForOneSong()
        {
            Song song = Helpers.SetupSongMock("Song", true);

            Playlist playlist = Helpers.SetupPlaylist(song);

            int index = playlist.GetIndexes(new[] { song }).Single();

            Assert.Equal(0, index);
        }

        [Fact]
        public void GetIndexesReturnsCorrectIndexesForOneSongWithMultipleReferences()
        {
            Song song = Helpers.SetupSongMock("Song", true);

            Playlist playlist = Helpers.SetupPlaylist(Enumerable.Repeat(song, 3));

            IEnumerable<int> indexes = playlist.GetIndexes(new[] { song });

            Assert.Equal(new[] { 0, 1, 2 }, indexes);
        }

        [Fact]
        public void GetIndexesReturnsNoIndexesForSongsThatAreNotInPlaylist()
        {
            Song[] songs = Helpers.SetupSongMocks(4, true);

            Playlist playlist = Helpers.SetupPlaylist(songs.Take(2));

            IEnumerable<int> indexes = playlist.GetIndexes(songs.Skip(2));

            Assert.Empty(indexes);
        }

        [Fact]
        public void IndexerThrowsArgumentOutOfRangeExceptionIfLessThanZero()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentOutOfRangeException>(() => playlist[-1]);
        }

        [Fact]
        public void IndexerThrowsArgumentOutOfRangeExceptionIfMoreThanZero()
        {
            Song song = Helpers.SetupSongMock();
            var playlist = Helpers.SetupPlaylist(song);

            Assert.Throws<ArgumentOutOfRangeException>(() => playlist[1]);
        }

        [Fact]
        public void MoveSongDownSmokeTest()
        {
            Song[] songs = Helpers.SetupSongMocks(5);
            var playlist = new Playlist("Playlist");
            playlist.AddSongs(songs);

            playlist.MoveSongDown(0);

            Assert.Equal(songs[0], playlist[1].Song);
        }

        [Fact]
        public void MoveSongDownValidatesRange()
        {
            var playlist = new Playlist("Playlist");
            playlist.AddSongs(Helpers.SetupSongMocks(5));

            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.MoveSongDown(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.MoveSongDown(-1));
        }

        [Fact]
        public void MoveSongUpSmokeTest()
        {
            Song[] songs = Helpers.SetupSongMocks(5);
            var playlist = new Playlist("Playlist");
            playlist.AddSongs(songs);

            playlist.MoveSongUp(4);

            Assert.Equal(songs[4], playlist[3].Song);
        }

        [Fact]
        public void MoveSongUpValidatesIndexRange()
        {
            var playlist = new Playlist("Playlist");
            playlist.AddSongs(Helpers.SetupSongMocks(5));

            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.MoveSongUp(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.MoveSongUp(5));
        }

        [Fact]
        public void NameSetterThrowsInvalidOperationExceptionIfIsTemporaryIsTrue()
        {
            var playlist = new Playlist("Playlist", true);

            Assert.Throws<InvalidOperationException>(() => playlist.Name = "Test");
        }

        [Fact]
        public void RemoveSongsCorrectsCurrentSongIndex()
        {
            Song[] songs = Helpers.SetupSongMocks(2);

            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.CurrentSongIndex.Value = 1;

            playlist.RemoveSongs(new[] { 0 });

            Assert.Equal(0, playlist.CurrentSongIndex.Value);
        }

        [Fact]
        public void RemoveSongsEnsuresOrderForMultipleRemovedSongs()
        {
            Song[] songs = Helpers.SetupSongMocks(7);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.RemoveSongs(new[] { 1, 3, 4 });

            Assert.Equal(4, playlist.Count());
            Assert.Equal(songs[0], playlist[0].Song);
            Assert.Equal(songs[2], playlist[1].Song);
            Assert.Equal(songs[5], playlist[2].Song);
            Assert.Equal(songs[6], playlist[3].Song);
        }

        [Fact]
        public void RemoveSongsEnsuresOrderForOneRemovedSong()
        {
            Song[] songs = Helpers.SetupSongMocks(4);
            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.RemoveSongs(new[] { 1 });

            Assert.Equal(3, playlist.Count());
            Assert.Equal(songs[0], playlist[0].Song);
            Assert.Equal(songs[2], playlist[1].Song);
            Assert.Equal(songs[3], playlist[2].Song);
        }

        [Fact]
        public void RemoveSongsThrowsArgumentNullExceptionifArgumentIsNull()
        {
            var playlist = new Playlist("Playlist");

            Assert.Throws<ArgumentNullException>(() => playlist.RemoveSongs(null));
        }

        [Fact]
        public void ShuffleMigratesCurrentSongIndex()
        {
            Song[] songs = Helpers.SetupSongMocks(100, true);

            Playlist playlist = Helpers.SetupPlaylist(songs);

            playlist.CurrentSongIndex.Value = 0;

            playlist.Shuffle();

            int newIndex = playlist.GetIndexes(new[] { songs[0] }).First();

            Assert.Equal(newIndex, playlist.CurrentSongIndex.Value);
        }
    }
}