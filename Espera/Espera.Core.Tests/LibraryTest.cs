using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Moq;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public sealed class LibraryTest
    {
        [Fact]
        public void AddAndSwitchToPlaylistSmokeTest()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.AddAndSwitchToPlaylist("Playlist", library.LocalAccessControl.RegisterLocalAccessToken());

                Assert.Equal("Playlist", library.CurrentPlaylist.Name);
                Assert.Equal("Playlist", library.Playlists.First().Name);
                Assert.Equal(1, library.Playlists.Count());
            }
        }

        [Fact]
        public void AddAndSwitchToPlaylistThrowsInvalidOperationExceptionIfPlaylistWithExistingNameIsAdded()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                library.AddAndSwitchToPlaylist("Playlist", token);

                Assert.Throws<InvalidOperationException>(() => library.AddAndSwitchToPlaylist("Playlist", token));
            }
        }

        [Fact]
        public void AddPlayistThrowInvalidOperationExceptionIfPlaylistWithExistingNameIsAdded()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddPlaylist("Playlist", token);

                Assert.Throws<InvalidOperationException>(() => library.AddPlaylist("Playlist", token));
            }
        }

        [Fact]
        public void AddPlaylistThrowsArgumentNullExceptionIfNameIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddPlaylist(null, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public void AddSongsToPlaylistThrowsAccessExceptionIfInPartyModeAndMultipleSongsAreAdded()
        {
            var songs = new[]
            {
                new Mock<Song>("TestPath", TimeSpan.Zero).Object,
                new Mock<Song>("TestPath", TimeSpan.Zero).Object
            };

            using (Library library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                Assert.Throws<AccessException>(() => library.AddSongsToPlaylist(songs, token));
            }
        }

        [Fact]
        public void AddSongsToPlaylistThrowsArgumentNullExceptionIfSongListIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddSongsToPlaylist(null, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public void AddSongToPlaylistThrowsArgumentNullExceptionIfSongIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddSongToPlaylist(null));
            }
        }

        [Fact]
        public async Task ChangeSongSourcePathSmokeTest()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            using (Library library = Helpers.CreateLibrary(fileSystem: fileSystem))
            {
                library.ChangeSongSourcePath("C://Test", library.LocalAccessControl.RegisterLocalAccessToken());
                Assert.Equal("C://Test", await library.SongSourcePath.FirstAsync());
            }
        }

        [Fact]
        public void ChangeSongSourcePathThrowsArgumentExceptionIfDirectoryDoesntExist()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.ChangeSongSourcePath("C://Test", library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public async Task ContinueSongCallsAudioPlayerPlay()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                bool called = false;
                library.AudioPlayerCallback.PlayRequest = () => called = true;

                Mock<Song> song = Helpers.CreateSongMock();

                library.AddSongToPlaylist(song.Object);

                await library.PlaySongAsync(0, token);

                await library.ContinueSongAsync(token);

                Assert.True(called);
            }
        }

        [Fact]
        public async Task ContinueSongThrowsAccessExceptionIfIsNotAdmin()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                await Helpers.ThrowsAsync<AccessException>(async () => await library.ContinueSongAsync(token));
            }
        }

        [Fact]
        public void GetPlaylistByNameReturnsNullIfPlaylistDoesNotExist()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Null(library.GetPlaylistByName("Playlist"));
            }
        }

        [Fact]
        public void GetPlaylistByNameThrowsArgumentNullExceptionIfPlaylistNameIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.GetPlaylistByName(null));
            }
        }

        [Fact]
        public async Task PauseSongCallsAudioPlayerPause()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                bool called = false;
                library.AudioPlayerCallback.PauseRequest = () => called = true;

                Mock<Song> song = Helpers.CreateSongMock();

                library.AddSongToPlaylist(song.Object);

                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                await library.PlaySongAsync(0, token);

                await library.PauseSongAsync(token);

                Assert.True(called);
            }
        }

        [Fact]
        public async Task PauseSongThrowsAccessExceptionIfIsNotAdministratorAndPausingIsLocked()
        {
            var settings = new CoreSettings
            {
                LockPlayPause = true
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                await Helpers.ThrowsAsync<AccessException>(async () => await library.PauseSongAsync(token));
            }
        }

        [Fact]
        public async Task PlayInstantlyAsyncValidatesRemoteAccess()
        {
            var settings = new CoreSettings
            {
                EnableRemoteControl = true,
                LockRemoteControl = true,
                RemoteControlPassword = "password"
            };

            Song[] songs = Helpers.SetupSongMocks(3);

            using (Library library = Helpers.CreateLibrary(settings))
            {
                Guid token = library.RemoteAccessControl.RegisterRemoteAccessToken();

                await Helpers.ThrowsAsync<AccessException>(async () => await library.PlayInstantlyAsync(songs, token));

                settings.LockRemoteControl = false;
                await library.PlayInstantlyAsync(songs, token);

                settings.EnableRemoteControl = false;
                await library.PlayInstantlyAsync(songs, token);
            }
        }

        [Fact]
        public async Task PlayInstantlyPlaysMultipleSongsInARow()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                var conn = library.SongStarted
                    .Take(2)
                    .PublishLast();
                conn.Connect();

                await library.PlayInstantlyAsync(Helpers.SetupSongMocks(2), library.LocalAccessControl.RegisterLocalAccessToken());

                await conn.Timeout(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task PlayInstantlySmokeTest()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                int called = 0;
                library.AudioPlayerCallback.PlayRequest = () => called++;
                Mock<Song> song = Helpers.CreateSongMock();

                await library.PlayInstantlyAsync(new[] { song.Object }, library.LocalAccessControl.RegisterLocalAccessToken());

                Assert.Equal(1, called);
            }
        }

        [Fact]
        public async Task PlayInstantlyThrowsArgumentNullExceptionIfSongListIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                await Helpers.ThrowsAsync<ArgumentNullException>(async () => await library.PlayInstantlyAsync(null, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public async Task PlayJumpsOverCorruptedSong()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                using (var handle = new CountdownEvent(2))
                {
                    library.AudioPlayerCallback.LoadRequest = () =>
                    {
                        switch (handle.CurrentCount)
                        {
                            case 2:
                                handle.Signal();
                                throw new SongLoadException();
                            case 1:
                                handle.Signal();
                                break;
                        }
                    };

                    Song[] songs = Helpers.SetupSongMocks(2);

                    await library.PlayInstantlyAsync(songs, library.LocalAccessControl.RegisterLocalAccessToken());

                    if (!handle.Wait(5000))
                    {
                        Assert.False(true, "Timeout");
                    }
                }
            }
        }

        [Fact]
        public async Task PlayNextSongThrowsAccessExceptionIfUserIsNotAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                await Helpers.ThrowsAsync<AccessException>(async () => await library.PlayNextSongAsync(token));
            }
        }

        [Fact]
        public async Task PlayPreviousSongThrowsInvalidOperationExceptionIfPlaylistIsEmpty()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                await Helpers.ThrowsAsync<InvalidOperationException>(async () => await library.PlayPreviousSongAsync(library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public async Task PlaySetsSongIsCorruptedToTrueIfFailing()
        {
            Func<Library, Task> test = async library =>
            {
                Song song = Helpers.SetupSongMock();

                library.AddSongToPlaylist(song);

                var observable = song.IsCorrupted.FirstAsync(x => x).PublishLast();
                observable.Connect();

                await library.PlaySongAsync(0, library.LocalAccessControl.RegisterLocalAccessToken());

                await observable.Timeout(TimeSpan.FromSeconds(10));

                Assert.True(song.IsCorrupted.Value);
            };

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AudioPlayerCallback.LoadRequest = () => { throw new SongLoadException(); };
                await test(library);
            }

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AudioPlayerCallback.PlayRequest = () => { throw new SongLoadException(); };
                await test(library);
            }
        }

        [Fact]
        public async Task PlaysNextSongAutomatically()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddSongsToPlaylist(Helpers.SetupSongMocks(2), token);

                var handle = new ManualResetEvent(false);
                int played = 0;

                library.SongStarted.Subscribe(x =>
                {
                    played++;

                    if (played == 2)
                    {
                        handle.Set();
                    }
                });

                await library.PlaySongAsync(0, token);

                if (!handle.WaitOne(5000))
                {
                    Assert.True(false, "Timeout");
                }
            }
        }

        [Fact]
        public async Task PlaySongThrowsAccessExceptionIfUserIsNotAdministratorAndLockPlayPauseIsTrue()
        {
            var settings = new CoreSettings
            {
                LockPlayPause = true
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                await Helpers.ThrowsAsync<AccessException>(async () => await library.PlaySongAsync(0, token));
            }
        }

        [Fact]
        public async Task PlaySongThrowsArgumentOutOfRangeExceptionIfIndexIsLessThanZero()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                await Helpers.ThrowsAsync<ArgumentOutOfRangeException>(async () => await library.PlaySongAsync(-1, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public void RemoveFromPlaylistAccessExceptionIfAccessModeIsPartyAndLockPlaylistRemovalIsTrue()
        {
            var songMock = new Mock<Song>("TestPath", TimeSpan.Zero);

            var settings = new CoreSettings
            {
                LockPlaylistRemoval = true
            };

            using (Library library = Helpers.CreateLibraryWithPlaylist(settings: settings))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddSongsToPlaylist(new[] { songMock.Object }, token);

                Assert.Throws<AccessException>(() => library.RemoveFromPlaylist(new[] { 0 }, token));
            }
        }

        [Fact]
        public void RemoveFromPlaylistByIndexesTest()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                Song[] songs = Helpers.SetupSongMocks(4);

                library.AddSongsToPlaylist(songs, token);

                library.RemoveFromPlaylist(new[] { 0, 2 }, token);

                Song[] remaining = library.CurrentPlaylist.Select(entry => entry.Song).ToArray();

                Assert.Equal(songs[1], remaining[0]);
                Assert.Equal(songs[3], remaining[1]);
            }
        }

        [Fact]
        public void RemoveFromPlaylistBySongReferenceTest()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                Song[] songs = Helpers.SetupSongMocks(4, true);

                library.AddSongsToPlaylist(songs, token);

                library.RemoveFromPlaylist(new[] { songs[0], songs[2] }, token);

                Song[] remaining = library.CurrentPlaylist.Select(entry => entry.Song).ToArray();

                Assert.Equal(songs[1], remaining[0]);
                Assert.Equal(songs[3], remaining[1]);
            }
        }

        [Fact]
        public void RemoveFromPlaylistThrowsArgumentNullExceptionIfIndexesIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<int>)null, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public void RemoveFromPlaylistThrowsArgumentNullExceptionIfSongListIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<Song>)null, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public async Task RemoveFromPlaylistWhileSongIsPlayingStopsCurrentSong()
        {
            bool finishedFired = false;

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AudioPlayerCallback.StopRequest = () => finishedFired = true;
                library.AddSongsToPlaylist(Helpers.SetupSongMocks(1), token);

                await library.PlaySongAsync(0, token);

                library.RemoveFromPlaylist(new[] { 0 }, token);
            }

            Assert.True(finishedFired);
        }

        [Fact]
        public void RemovePlaylistSmokeTest()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddPlaylist("Playlist", token);

                library.RemovePlaylist(library.GetPlaylistByName("Playlist"), token);

                Assert.Empty(library.Playlists);
            }
        }

        [Fact]
        public void RemovePlaylistThrowsArgumentNullExceptionIfPlaylistNameIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemovePlaylist(null, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }

        [Fact]
        public async Task SaveDoesNotSaveTemporaryPlaylist()
        {
            var libraryWriter = new Mock<ILibraryWriter>();
            libraryWriter.Setup(x => x.Write(It.IsAny<IEnumerable<LocalSong>>(), It.IsAny<IEnumerable<Playlist>>(), It.IsAny<string>()))
                .Callback<IEnumerable<LocalSong>, IEnumerable<Playlist>, string>((songs, playlists, songSourcePath) =>
                    Assert.Equal(1, playlists.Count()));

            using (Library library = Helpers.CreateLibrary(libraryWriter.Object))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddAndSwitchToPlaylist("Playlist", token);

                await library.PlayInstantlyAsync(Helpers.SetupSongMocks(1), token);

                library.Save();
            }

            libraryWriter.Verify(x => x.Write(It.IsAny<IEnumerable<LocalSong>>(), It.IsAny<IEnumerable<Playlist>>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public async Task SwitchingPlaylistAndPlayingSongsChangesCurrentSongIndex()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddSongToPlaylist(Helpers.SetupSongMock());

                await library.PlaySongAsync(0, token);

                library.AddPlaylist("Playlist 2", token);
                library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2"), token);
                library.AddSongToPlaylist(Helpers.SetupSongMock());

                await library.PlaySongAsync(0, token);

                library.SwitchToPlaylist(library.GetPlaylistByName("Playlist"), token);

                Assert.Equal(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex.Value);
                Assert.Equal(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex.Value);
            }
        }

        [Fact]
        public async Task SwitchingPlaylistPreventsNextSongFromPlaying()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                int played = 0;

                library.AudioPlayerCallback.PlayRequest = () =>
                {
                    if (played == 0)
                    {
                        library.AddAndSwitchToPlaylist("Playlist2", token);
                    }

                    played++;
                };

                library.AddSongsToPlaylist(Helpers.SetupSongMocks(2), token);

                await library.PlaySongAsync(0, token);

                Assert.Equal(1, played);
            }
        }

        [Fact]
        public async Task SwitchToPlaylistSetsCurrentSongIndexIfChangingToOtherPlaylistAndPlayingFirstSong()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddSongToPlaylist(Helpers.SetupSongMock());

                await library.PlaySongAsync(0, token);

                library.AddPlaylist("Playlist 2", token);
                library.SwitchToPlaylist(library.Playlists.Last(), token);
                library.AddSongToPlaylist(Helpers.SetupSongMock());

                await library.PlaySongAsync(0, token);

                Assert.Equal(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex.Value);
                Assert.Equal(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex.Value);
            }
        }

        [Fact]
        public void SwitchToPlaylistThrowsAccessExceptionIfPartyModeAndLockPlaylistSwitchingIsTrue()
        {
            var settings = new CoreSettings
            {
                LockPlaylistSwitching = true
            };

            using (Library library = Helpers.CreateLibraryWithPlaylist("Playlist 1", settings))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddPlaylist("Playlist 2", token);

                Assert.Throws<AccessException>(() => library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2"), token));
            }
        }

        [Fact]
        public void SwitchToPlaylistThrowsArgumentNullExceptionIfPlaylistIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.SwitchToPlaylist(null, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }
    }
}