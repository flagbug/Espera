using System;
using System.Linq;
using Espera.Core.Management;
using Xunit;

namespace Espera.Core.Tests
{
    public sealed class PlaylistTest
    {
        [Fact]
        public void VotesAfterCurrentSongIndexDontResetWhenCurrentSongIndexAdvances()
        {
            var playlist = new Playlist("Playlist");
            playlist.AddSongs(Helpers.SetupSongMocks(3));

            playlist.VoteFor(0);
            playlist.VoteFor(1);
            playlist.VoteFor(2);

            playlist.CurrentSongIndex = 1;

            Assert.Equal(1, playlist[1].Votes);
            Assert.Equal(1, playlist[2].Votes);
        }

        [Fact]
        public void VotesResetWhenCurrentSongIndexAdvances()
        {
            var playlist = new Playlist("Playlist");
            playlist.AddSongs(Helpers.SetupSongMocks(3));

            playlist.VoteFor(0);

            playlist.CurrentSongIndex = 0;

            Assert.Equal(1, playlist[0].Votes);

            playlist.CurrentSongIndex = 1;

            Assert.Equal(0, playlist[0].Votes);

            playlist.VoteFor(2);

            playlist.CurrentSongIndex = 2;

            Assert.Equal(0, playlist[1].Votes);
        }

        [Fact]
        public void VotesRespectCurrentSongIndex()
        {
            var playlist = new Playlist("Playlist");
            playlist.AddSongs(Helpers.SetupSongMocks(5));
            var entries = playlist.ToList();
            var expectedOrder = new[] { entries[0], entries[1], entries[3], entries[4], entries[2] };

            playlist.CurrentSongIndex = 1;

            playlist.VoteFor(4);
            playlist.VoteFor(4);
            playlist.VoteFor(3);

            Assert.Equal(playlist, expectedOrder);
        }

        public class TheAddSongsMethod
        {
            [Fact]
            public void SetsCorrectPlaylistIndexes()
            {
                var songs = Helpers.SetupSongMocks(2);

                var playlist = new Playlist("Playlist");

                playlist.AddSongs(songs);

                Assert.Equal(0, playlist[0].Index);
                Assert.Equal(1, playlist[1].Index);
            }

            [Fact]
            public void ShouldThrowArgumentNullExceptionIfArgumentIsNull()
            {
                var playlist = new Playlist("Playlist");

                Assert.Throws<ArgumentNullException>(() => playlist.AddSongs(null));
            }

            [Fact]
            public void SmokeTest()
            {
                var songs = Helpers.SetupSongMocks(4);
                var playlist = Helpers.SetupPlaylist(songs);

                Assert.Equal(4, playlist.Count());
                Assert.Equal(songs[0], playlist[0].Song);
                Assert.Equal(songs[1], playlist[1].Song);
                Assert.Equal(songs[2], playlist[2].Song);
                Assert.Equal(songs[3], playlist[3].Song);
            }
        }

        public class TheCanPlayNextSongProperty
        {
            [Fact]
            public void ReturnsFalseIfCurrentSongIndexIsLastSong()
            {
                var songs = Helpers.SetupSongMocks(4);
                var playlist = Helpers.SetupPlaylist(songs);

                playlist.CurrentSongIndex = 3;

                Assert.False(playlist.CanPlayNextSong);
            }

            [Fact]
            public void ReturnsFalseIfCurrentSongIndexIsNull()
            {
                var songs = Helpers.SetupSongMocks(4);
                var playlist = Helpers.SetupPlaylist(songs);

                playlist.CurrentSongIndex = null;

                Assert.False(playlist.CanPlayNextSong);
            }

            [Fact]
            public void ReturnsFalseIfPlaylistIsEmpty()
            {
                var playlist = new Playlist("Playlist");

                Assert.False(playlist.CanPlayNextSong);
            }

            [Fact]
            public void ReturnsTrueIfCurrentSongIndexIsZero()
            {
                var songs = Helpers.SetupSongMocks(4);
                var playlist = Helpers.SetupPlaylist(songs);

                playlist.CurrentSongIndex = 0;

                Assert.True(playlist.CanPlayNextSong);
            }
        }

        public class TheCanPlayPreviousSongProperty
        {
            [Fact]
            public void ReturnsFalseIfCurrentSongIndexIsNull()
            {
                var songs = Helpers.SetupSongMocks(4);
                var playlist = Helpers.SetupPlaylist(songs);

                playlist.CurrentSongIndex = null;

                Assert.False(playlist.CanPlayPreviousSong);
            }

            [Fact]
            public void ReturnsFalseIfCurrentSongIndexIsZero()
            {
                var songs = Helpers.SetupSongMocks(4);
                var playlist = Helpers.SetupPlaylist(songs);

                playlist.CurrentSongIndex = 0;

                Assert.False(playlist.CanPlayPreviousSong);
            }

            [Fact]
            public void ReturnsFalseIfPlaylistIsEmpty()
            {
                var playlist = new Playlist("Playlist");

                Assert.False(playlist.CanPlayPreviousSong);
            }

            [Fact]
            public void ReturnsTrueIfCurrentSongIndexIsLastSong()
            {
                var songs = Helpers.SetupSongMocks(4);
                var playlist = Helpers.SetupPlaylist(songs);

                playlist.CurrentSongIndex = 3;

                Assert.True(playlist.CanPlayPreviousSong);
            }
        }

        public class TheCurrentSongIndexProperty
        {
            [Fact]
            public void SetterSmokeTest()
            {
                new Playlist("Playlist").CurrentSongIndex = null;
            }

            [Fact]
            public void SetterThrowsArgumentOutOfRangeExceptionIfSetToZeroWhilePlaylistIsEmpty()
            {
                var playlist = new Playlist("Playlist");

                Assert.Throws<ArgumentOutOfRangeException>(() => playlist.CurrentSongIndex = 0);
            }

            [Fact]
            public void SetterThrowsArgumentOutOfRangeExceptionIfValueIsNotInPlaylistRange()
            {
                var songs = Helpers.SetupSongMocks(3);
                var playlist = Helpers.SetupPlaylist(songs);

                Assert.Throws<ArgumentOutOfRangeException>(() => playlist.CurrentSongIndex = 3);
            }
        }

        public class TheGetIndexesMethod
        {
            [Fact]
            public void ReturnsCorrectIndexesForMultipleSongs()
            {
                var songs = Helpers.SetupSongMocks(3);

                var playlist = Helpers.SetupPlaylist(songs);

                var indexes = playlist.GetIndexes(songs);

                Assert.Equal(new[] { 0, 1, 2 }, indexes);
            }

            [Fact]
            public void ReturnsCorrectIndexesForOneSong()
            {
                var song = Helpers.SetupSongMock();

                var playlist = Helpers.SetupPlaylist(song);

                var index = playlist.GetIndexes(new[] { song }).Single();

                Assert.Equal(0, index);
            }

            [Fact]
            public void ReturnsCorrectIndexesForOneSongWithMultipleReferences()
            {
                var song = Helpers.SetupSongMock();

                var playlist = Helpers.SetupPlaylist(Enumerable.Repeat(song, 3));

                var indexes = playlist.GetIndexes(new[] { song });

                Assert.Equal(new[] { 0, 1, 2 }, indexes);
            }

            [Fact]
            public void ReturnsNoIndexesForSongsThatAreNotInPlaylist()
            {
                var songs = Helpers.SetupSongMocks(4);

                var playlist = Helpers.SetupPlaylist(songs.Take(2));

                var indexes = playlist.GetIndexes(songs.Skip(2));

                Assert.Empty(indexes);
            }
        }

        public class TheIndexer
        {
            [Fact]
            public void ThrowsArgumentOutOfRangeExceptionIfLessThanZero()
            {
                var playlist = new Playlist("Playlist");

                Assert.Throws<ArgumentOutOfRangeException>(() => playlist[-1]);
            }

            [Fact]
            public void ThrowsArgumentOutOfRangeExceptionIfMoreThanZero()
            {
                var song = Helpers.SetupSongMock();
                var playlist = Helpers.SetupPlaylist(song);

                Assert.Throws<ArgumentOutOfRangeException>(() => playlist[1]);
            }
        }

        public class TheMoveSongMethod
        {
            [Fact]
            public void CanDecrementIndex()
            {
                var songs = Helpers.SetupSongMocks(3);
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(songs);

                playlist.MoveSong(2, 1);

                Assert.Equal(songs[0], playlist[0].Song);
                Assert.Equal(songs[1], playlist[2].Song);
                Assert.Equal(songs[2], playlist[1].Song);
            }

            [Fact]
            public void CanIncrementIndex()
            {
                var songs = Helpers.SetupSongMocks(3);
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(songs);

                playlist.MoveSong(0, 1);

                Assert.Equal(songs[0], playlist[1].Song);
                Assert.Equal(songs[1], playlist[0].Song);
                Assert.Equal(songs[2], playlist[2].Song);
            }

            [Fact]
            public void SmokeTest()
            {
                var songs = Helpers.SetupSongMocks(3);
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(songs);

                playlist.MoveSong(0, 2);

                Assert.Equal(songs[1], playlist[0].Song);
                Assert.Equal(songs[2], playlist[1].Song);
                Assert.Equal(songs[0], playlist[2].Song);
            }

            [Fact]
            public void ValidatesArguments()
            {
                var songs = Helpers.SetupSongMocks(2);
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(songs);

                Assert.Throws<ArgumentOutOfRangeException>(() => playlist.MoveSong(-1, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => playlist.MoveSong(2, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => playlist.MoveSong(0, 2));
                Assert.Throws<ArgumentOutOfRangeException>(() => playlist.MoveSong(0, -1));
            }
        }

        public class TheNameProperty
        {
            [Fact]
            public void SetterThrowsInvalidOperationExceptionIfIsTemporaryIsTrue()
            {
                var playlist = new Playlist("Playlist", true);

                Assert.Throws<InvalidOperationException>(() => playlist.Name = "Test");
            }
        }

        public class TheRemoveSongsMethod
        {
            [Fact]
            public void CorrectsCurrentSongIndex()
            {
                var songs = Helpers.SetupSongMocks(2);

                var playlist = Helpers.SetupPlaylist(songs);

                playlist.CurrentSongIndex = 1;

                playlist.RemoveSongs(new[] { 0 });

                Assert.Equal(0, playlist.CurrentSongIndex);
            }

            [Fact]
            public void EnsuresOrderForMultipleRemovedSongs()
            {
                var songs = Helpers.SetupSongMocks(7);
                var playlist = Helpers.SetupPlaylist(songs);

                playlist.RemoveSongs(new[] { 1, 3, 4 });

                Assert.Equal(4, playlist.Count());
                Assert.Equal(songs[0], playlist[0].Song);
                Assert.Equal(songs[2], playlist[1].Song);
                Assert.Equal(songs[5], playlist[2].Song);
                Assert.Equal(songs[6], playlist[3].Song);
            }

            [Fact]
            public void EnsuresOrderForOneRemovedSong()
            {
                var songs = Helpers.SetupSongMocks(4);
                var playlist = Helpers.SetupPlaylist(songs);

                playlist.RemoveSongs(new[] { 1 });

                Assert.Equal(3, playlist.Count());
                Assert.Equal(songs[0], playlist[0].Song);
                Assert.Equal(songs[2], playlist[1].Song);
                Assert.Equal(songs[3], playlist[2].Song);
            }

            [Fact]
            public void ThrowsArgumentNullExceptionifArgumentIsNull()
            {
                var playlist = new Playlist("Playlist");

                Assert.Throws<ArgumentNullException>(() => playlist.RemoveSongs(null));
            }
        }

        public class TheShuffleMethod
        {
            [Fact]
            public void MigratesCurrentSongIndex()
            {
                var songs = Helpers.SetupSongMocks(100);

                var playlist = Helpers.SetupPlaylist(songs);

                playlist.CurrentSongIndex = 0;

                playlist.Shuffle();

                var newIndex = playlist.GetIndexes(new[] { songs[0] }).First();

                Assert.Equal(newIndex, playlist.CurrentSongIndex);
            }
        }

        public class TheVoteForMethod
        {
            [Fact]
            public void ChecksIndexBounds()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(1));

                Assert.Throws<ArgumentOutOfRangeException>(() => playlist.VoteFor(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => playlist.VoteFor(2));
            }

            [Fact]
            public void EntryBeforeCurrentSongWorks()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(2));

                playlist.CurrentSongIndex = 0;

                playlist.VoteFor(1);

                Assert.Equal(1, playlist[1].Votes);
            }

            [Fact]
            public void FirstEntryLeavesItInFirstPlace()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(2));
                var snapshot = playlist.ToList();

                playlist.VoteFor(0);

                Assert.Equal(snapshot, playlist);
            }

            [Fact]
            public void IncreasesVoteCount()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(1));

                playlist.VoteFor(0);

                Assert.Equal(1, playlist[0].Votes);
            }

            [Fact]
            public void IsFirstInFirstOut()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(5));

                playlist.VoteFor(4);

                var entry1 = playlist[4];
                playlist.VoteFor(4);

                Assert.Equal(1, entry1.Index);

                var entry2 = playlist[4];
                playlist.VoteFor(4);

                Assert.Equal(2, entry2.Index);
            }

            [Fact]
            public void LeavesEntryInSamePlaceIfNextEntryHasSameVoteCount()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(2));
                var snapshot = playlist.ToList();

                playlist.VoteFor(0);
                playlist.VoteFor(1);

                Assert.Equal(snapshot, playlist);
            }

            [Fact]
            public void SmokeTest()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(4));
                var snapShot = playlist.ToList();
                var expectedOrder = new[] { snapShot[3], snapShot[2], snapShot[0], snapShot[1] };

                playlist.VoteFor(3);
                playlist.VoteFor(0);

                playlist.VoteFor(3);
                playlist.VoteFor(2);

                Assert.Equal(expectedOrder, playlist);
            }

            [Fact]
            public void WithIndexThatEqualsCurrentSongIndexThrowsInvalidOperationException()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(3));

                playlist.CurrentSongIndex = 1;

                Assert.Throws<InvalidOperationException>(() => playlist.VoteFor(1));
            }

            [Fact]
            public void WithIndexThatIsLessThanCurrentSongIndexThrowsInvalidOperationException()
            {
                var playlist = new Playlist("Playlist");
                playlist.AddSongs(Helpers.SetupSongMocks(3));

                playlist.CurrentSongIndex = 1;

                Assert.Throws<InvalidOperationException>(() => playlist.VoteFor(0));
            }
        }
    }
}