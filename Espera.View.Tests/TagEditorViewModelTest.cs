using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using ReactiveUI;
using Xunit;

namespace Espera.View.Tests
{
    public class TagEditorViewModelTest
    {
        public class TheAlbumProperty
        {
            [Fact]
            public void MultipleSongsWithDifferentAlbumReturnsEmptyString()
            {
                var songs = new[] { Helpers.LocalSong1, Helpers.LocalSong2 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(string.Empty, fixture.Album);
            }

            [Fact]
            public void MultipleSongsWithSameArtistReturnsCommonAlbum()
            {
                var song1 = new LocalSong("C://song1.mp3", TimeSpan.Zero)
                {
                    Album = "The Album"
                };

                var song2 = new LocalSong("C://song2.mp3", TimeSpan.Zero)
                {
                    Album = "The Album"
                };

                var songs = new[] { song1, song2 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(song1.Album, fixture.Album);
            }

            [Fact]
            public void ReturnsCustomValueIfSet()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true))
                {
                    Album = "Custom Album"
                };

                Assert.Equal("Custom Album", fixture.Album);
            }

            [Fact]
            public void SingleSongReturnsSongAlbum()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(Helpers.LocalSong1.Album, fixture.Album);
            }
        }

        public class TheArtistProperty
        {
            [Fact]
            public void MultipleSongsWithDifferentArtistReturnsEmptyString()
            {
                var songs = new[] { Helpers.LocalSong1, Helpers.LocalSong2 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(string.Empty, fixture.Artist);
            }

            [Fact]
            public void MultipleSongsWithSameArtistReturnsCommonArtist()
            {
                var song1 = new LocalSong("C://song1.mp3", TimeSpan.Zero)
                {
                    Artist = "The Artist"
                };

                var song2 = new LocalSong("C://song2.mp3", TimeSpan.Zero)
                {
                    Artist = "The Artist"
                };

                var songs = new[] { song1, song2 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(song1.Artist, fixture.Artist);
            }

            [Fact]
            public void ReturnsCustomValueIfSet()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true))
                {
                    Artist = "Custom Artist"
                };

                Assert.Equal("Custom Artist", fixture.Artist);
            }

            [Fact]
            public void SingleSongReturnsSongArtist()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(Helpers.LocalSong1.Artist, fixture.Artist);
            }
        }

        public class TheFinishedProperty
        {
            [Fact]
            public async Task FiresWhenCancelCommandInvoked()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                var finished = fixture.Finished.CreateCollection();

                await fixture.Cancel.ExecuteAsync();

                Assert.Equal(1, finished.Count);
            }
        }

        public class TheGenreProperty
        {
            [Fact]
            public void MultipleSongsWithDifferentAlbumReturnsEmptyString()
            {
                var songs = new[] { Helpers.LocalSong1, Helpers.LocalSong2 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(string.Empty, fixture.Genre);
            }

            [Fact]
            public void MultipleSongsWithSameArtistReturnsCommonAlbum()
            {
                var song1 = new LocalSong("C://song1.mp3", TimeSpan.Zero)
                {
                    Genre = "The Genre"
                };

                var song2 = new LocalSong("C://song2.mp3", TimeSpan.Zero)
                {
                    Genre = "The Genre"
                };

                var songs = new[] { song1, song2 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(song1.Genre, fixture.Genre);
            }

            [Fact]
            public void ReturnsCustomValueIfSet()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true))
                {
                    Genre = "Custom Genre"
                };

                Assert.Equal("Custom Genre", fixture.Genre);
            }

            [Fact]
            public void SingleSongReturnsSongAlbum()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(Helpers.LocalSong1.Genre, fixture.Genre);
            }
        }

        public class TheSaveCommand
        {
            [Fact]
            public async Task InvokesWarningIfMoreThanOneSong()
            {
                bool called = false;
                var songs = new[] { Helpers.LocalSong1, Helpers.LocalSong2 };
                var fixture = new TagEditorViewModel(songs, () => Task.Run(() =>
                {
                    called = true;
                    return false;
                }));

                await fixture.Save.ExecuteAsync();

                Assert.True(called);
            }
        }

        public class TheTitleProperty
        {
            [Fact]
            public void ReturnsCustomValueIfSet()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true))
                {
                    Title = "Custom Title"
                };

                Assert.Equal("Custom Title", fixture.Title);
            }

            [Fact]
            public void SingleSongReturnsSongTitle()
            {
                var songs = new[] { Helpers.LocalSong1 };
                var fixture = new TagEditorViewModel(songs, () => Task.FromResult(true));

                Assert.Equal(Helpers.LocalSong1.Title, fixture.Title);
            }
        }
    }
}