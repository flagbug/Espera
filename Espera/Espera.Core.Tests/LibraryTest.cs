using Akavache;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
                library.AddAndSwitchToPlaylist("Playlist");

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
                library.AddAndSwitchToPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.AddAndSwitchToPlaylist("Playlist"));
            }
        }

        [Fact]
        public void AddPlayistThrowInvalidOperationExceptionIfPlaylistWithExistingNameIsAdded()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.AddPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.AddPlaylist("Playlist"));
            }
        }

        [Fact]
        public void AddPlaylistThrowsArgumentNullExceptionIfNameIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddPlaylist(null));
            }
        }

        [Fact]
        public void AddSongsToPlaylistThrowsArgumentNullExceptionIfSongListIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddSongsToPlaylist(null));
            }
        }

        [Fact]
        public void AddSongsToPlaylistThrowsInvalidOperationExceptionIfInPartyModeAndMultipleSongsAreAdded()
        {
            var songs = new[]
            {
                new Mock<Song>("TestPath", TimeSpan.Zero).Object,
                new Mock<Song>("TestPath", TimeSpan.Zero).Object
            };

            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.AddSongsToPlaylist(songs));
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
        public void CanAddSongToPlaylistReturnsFalseIfIsPartyModeAndRemainingTimeIsBiggerThanZero()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.PlaylistTimeout = TimeSpan.FromMinutes(1);

                library.CreateAdmin("Password");
                library.ChangeToParty();

                Song song = Helpers.SetupSongMock();

                library.AddSongToPlaylist(song);

                Assert.False(library.CanAddSongToPlaylist);
            }
        }

        [Fact]
        public void CanAddSongToPlaylistReturnsTrueIfIsAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.True(library.CanAddSongToPlaylist);
            }
        }

        [Fact]
        public async Task CanChangeTimeIsFalseIfIsNotAdministratorAndLockTimeIsTrue()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockTime = true
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.False(await library.CanChangeTime.FirstAsync());
            }
        }

        [Fact]
        public async Task CanChangeTimeIsTrueIfIsAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.True(await library.CanChangeTime.FirstAsync());
            }
        }

        [Fact]
        public async Task CanChangeTimeIsTrueIfIsNotAdministratorAndLockTimeIsFalse()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockTime = false
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.True(await library.CanChangeTime.FirstAsync());
            }
        }

        [Fact]
        public async Task CanChangeVolumeIsFalseIsNotAdministratorAndLockVolumeIsTrue()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockVolume = true
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.False(await library.CanChangeVolume.FirstAsync());
            }
        }

        [Fact]
        public async Task CanChangeVolumeIsTrueIfIsAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.True(await library.CanChangeVolume.FirstAsync());
            }
        }

        [Fact]
        public async Task CanChangeVolumeIsTrueIfIsNotAdministratorAndLockVolumeIsFalse()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockVolume = false
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.True(await library.CanChangeVolume.FirstAsync());
            }
        }

        [Fact]
        public async Task CanSwitchPlaylistIsFalseIfIsNotAdministratorAndLockPlaylistSwitchingIsTrue()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockPlaylistSwitching = true
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.False(await library.CanSwitchPlaylist.FirstAsync());
            }
        }

        [Fact]
        public async Task CanSwitchPlaylistIsTrueIfIsNotAdministratorAndLockPlaylistSwitchingIsFalse()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockPlaylistSwitching = false
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.True(await library.CanSwitchPlaylist.FirstAsync());
            }
        }

        [Fact]
        public async Task CanSwitchPlaylistIsTrueIsAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.True(await library.CanSwitchPlaylist.FirstAsync());
            }
        }

        [Fact]
        public async Task ChangeSongSourcePathSmokeTest()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            using (Library library = Helpers.CreateLibrary(fileSystem))
            {
                library.ChangeSongSourcePath("C://Test");
                Assert.Equal("C://Test", await library.SongSourcePath.FirstAsync());
            }
        }

        [Fact]
        public void ChangeSongSourcePathThrowsArgumentExceptionIfDirectoryDoesntExist()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.ChangeSongSourcePath("C://Test"));
            }
        }

        [Fact]
        public async Task ChangeToAdminChangesAccessModeToAdministratorIfPasswordIsCorrect()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToAdmin("TestPassword");

                Assert.Equal(AccessMode.Administrator, await library.AccessMode.FirstAsync());
            }
        }

        [Fact]
        public void ChangeToAdminThrowsArgumentNullExceptionIfPasswordIsNull_()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.ChangeToAdmin(null));
            }
        }

        [Fact]
        public void ChangeToAdminThrowsWrongPasswordExceptionPasswordIsIncorrect()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");

                Assert.Throws<WrongPasswordException>(() => library.ChangeToAdmin("WrongPassword"));
            }
        }

        [Fact]
        public void ChangeToPartyThrowsInvalidOperationExceptionIfAdministratorIsNotCreated()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<InvalidOperationException>(() => library.ChangeToParty());
            }
        }

        [Fact]
        public async Task ContinueSongCallsAudioPlayerPlay()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                bool called = false;
                library.AudioPlayerCallback.PlayRequest = () => called = true;

                Mock<Song> song = Helpers.CreateSongMock();

                library.AddSongToPlaylist(song.Object);

                await library.PlaySongAsync(0);

                await library.ContinueSongAsync();

                Assert.True(called);
            }
        }

        [Fact]
        public void ContinueSongThrowsInvalidOperationExceptionIfIsNotAdmin()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(async () => await library.ContinueSongAsync());
            }
        }

        [Fact]
        public void CreateAdminSetsIsAdministratorCreatedToTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");

                Assert.True(library.IsAdministratorCreated);
            }
        }

        [Fact]
        public void CreateAdminThrowsArgumentExceptionIfPasswordIsEmpty()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(String.Empty));
            }
        }

        [Fact]
        public void CreateAdminThrowsArgumentExceptionIfPasswordIsWhiteSpace()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(" "));
            }
        }

        [Fact]
        public void CreateAdminThrowsArgumentNullExceptionIfPasswordIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.CreateAdmin(null));
            }
        }

        [Fact]
        public void CreateAdminThrowsInvalidOperationExceptionIfAdminIsAlreadyCreated()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("Password");
                Assert.Throws<InvalidOperationException>(() => library.CreateAdmin("Password"));
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
        public async Task InitializeFiresInitalUpdateAfterLibraryLoad()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            var readerFired = new Subject<int>();
            var reader = new Mock<ILibraryReader>();
            reader.SetupGet(x => x.LibraryExists).Returns(true);
            reader.Setup(x => x.ReadPlaylists()).Returns(new List<Playlist>());
            reader.Setup(x => x.ReadSongSourcePath()).Returns(String.Empty);
            reader.Setup(x => x.ReadSongs()).Callback(() => readerFired.OnNext(1)).Returns(new List<LocalSong>()).Verifiable();

            using (var library = Helpers.CreateLibrary(null, reader.Object, null, fileSystem))
            {
                library.ChangeSongSourcePath("C://Test");

                var isUpdating = library.IsUpdating.FirstAsync(x => x).Select(x => 2).PublishLast();
                isUpdating.Connect();

                var first = readerFired.Amb(isUpdating).FirstAsync().PublishLast();
                first.Connect();

                library.Initialize();

                Assert.Equal(1, await first.Timeout(TimeSpan.FromSeconds(5)));
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

                await library.PlaySongAsync(0);

                await library.PauseSongAsync();

                Assert.True(called);
            }
        }

        [Fact]
        public void PauseSongThrowsInvalidOperationExceptionIfIsNotAdministratorAndPausingIsLocked()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockPlayPause = true
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(async () => await library.PauseSongAsync());
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

                await library.PlayInstantlyAsync(Helpers.SetupSongMocks(2));

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

                await library.PlayInstantlyAsync(new[] { song.Object });

                Assert.Equal(1, called);
            }
        }

        [Fact]
        public void PlayInstantlyThrowsArgumentNullExceptionIfSongListIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(async () => await library.PlayInstantlyAsync(null));
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

                    await library.PlayInstantlyAsync(songs);

                    if (!handle.Wait(5000))
                    {
                        Assert.False(true, "Timeout");
                    }
                }
            }
        }

        [Fact]
        public void PlayNextSongThrowsInvalidOperationExceptionIfUserIsNotAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(async () => await library.PlayNextSongAsync());
            }
        }

        [Fact]
        public void PlayPreviousSongThrowsInvalidOperationExceptionIfPlaylistIsEmpty()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Assert.Throws<InvalidOperationException>(async () => await library.PlayPreviousSongAsync());
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

                await library.PlaySongAsync(0);

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
                library.AddSongsToPlaylist(Helpers.SetupSongMocks(2));

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

                await library.PlaySongAsync(0);

                if (!handle.WaitOne(5000))
                {
                    Assert.True(false, "Timeout");
                }
            }
        }

        [Fact]
        public void PlaySongThrowsArgumentOutOfRangeExceptionIfIndexIsLessThanZero()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentOutOfRangeException>(async () => await library.PlaySongAsync(-1));
            }
        }

        [Fact]
        public void PlaySongThrowsInvalidOperationExceptionIfUserIsNotAdministratorAndLockPlayPauseIsTrue()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockPlayPause = true
            };

            using (Library library = Helpers.CreateLibrary(settings))
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(async () => await library.PlaySongAsync(0));
            }
        }

        [Fact]
        public void RemoveFromPlaylistByIndexesTest()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Song[] songs = Helpers.SetupSongMocks(4);

                library.AddSongsToPlaylist(songs);

                library.RemoveFromPlaylist(new[] { 0, 2 });

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
                Song[] songs = Helpers.SetupSongMocks(4, true);

                library.AddSongsToPlaylist(songs);

                library.RemoveFromPlaylist(new[] { songs[0], songs[2] });

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
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<int>)null));
            }
        }

        [Fact]
        public void RemoveFromPlaylistThrowsArgumentNullExceptionIfSongListIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<Song>)null));
            }
        }

        [Fact]
        public void RemoveFromPlaylistThrowsInvalidOperationExceptionIfAccessModeIsPartyAndLockPlaylistRemovalIsTrue()
        {
            var songMock = new Mock<Song>("TestPath", TimeSpan.Zero);

            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockPlaylistRemoval = true
            };

            using (Library library = Helpers.CreateLibraryWithPlaylist(settings: settings))
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

                library.CreateAdmin("SomePassword");

                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.RemoveFromPlaylist(new[] { 0 }));
            }
        }

        [Fact]
        public async Task RemoveFromPlaylistWhileSongIsPlayingStopsCurrentSong()
        {
            bool finishedFired = false;

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AudioPlayerCallback.StopRequest = () => finishedFired = true;
                library.AddSongsToPlaylist(Helpers.SetupSongMocks(1));

                await library.PlaySongAsync(0);

                library.RemoveFromPlaylist(new[] { 0 });
            }

            Assert.True(finishedFired);
        }

        [Fact]
        public void RemovePlaylistSmokeTest()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.AddPlaylist("Playlist");

                library.RemovePlaylist(library.GetPlaylistByName("Playlist"));

                Assert.Empty(library.Playlists);
            }
        }

        [Fact]
        public void RemovePlaylistThrowsArgumentNullExceptionIfPlaylistNameIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemovePlaylist(null));
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
                library.AddAndSwitchToPlaylist("Playlist");

                await library.PlayInstantlyAsync(Helpers.SetupSongMocks(1));

                library.Save();
            }

            libraryWriter.Verify(x => x.Write(It.IsAny<IEnumerable<LocalSong>>(), It.IsAny<IEnumerable<Playlist>>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public async Task SwitchingPlaylistAndPlayingSongsChangesCurrentSongIndex()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongToPlaylist(Helpers.SetupSongMock());

                await library.PlaySongAsync(0);

                library.AddPlaylist("Playlist 2");
                library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2"));
                library.AddSongToPlaylist(Helpers.SetupSongMock());

                await library.PlaySongAsync(0);

                library.SwitchToPlaylist(library.GetPlaylistByName("Playlist"));

                Assert.Equal(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex.Value);
                Assert.Equal(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex.Value);
            }
        }

        [Fact]
        public async Task SwitchingPlaylistPreventsNextSongFromPlaying()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                int played = 0;

                library.AudioPlayerCallback.PlayRequest = () =>
                {
                    if (played == 0)
                    {
                        library.AddAndSwitchToPlaylist("Playlist2");
                    }

                    played++;
                };

                library.AddSongsToPlaylist(Helpers.SetupSongMocks(2));

                await library.PlaySongAsync(0);

                Assert.Equal(1, played);
            }
        }

        [Fact]
        public async Task SwitchToPlaylistSetsCurrentSongIndexIfChangingToOtherPlaylistAndPlayingFirstSong()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongToPlaylist(Helpers.SetupSongMock());

                await library.PlaySongAsync(0);

                library.AddPlaylist("Playlist 2");
                library.SwitchToPlaylist(library.Playlists.Last());
                library.AddSongToPlaylist(Helpers.SetupSongMock());

                await library.PlaySongAsync(0);

                Assert.Equal(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex.Value);
                Assert.Equal(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex.Value);
            }
        }

        [Fact]
        public void SwitchToPlaylistThrowsArgumentNullExceptionIfPlaylistIsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.SwitchToPlaylist(null));
            }
        }

        [Fact]
        public void SwitchToPlaylistThrowsInvalidOperationExceptionIfPartyModeAndLockPlaylistSwitchingIsTrue()
        {
            var settings = new CoreSettings(BlobCache.InMemory)
            {
                LockPlaylistSwitching = true
            };

            using (Library library = Helpers.CreateLibraryWithPlaylist("Playlist 1", settings))
            {
                library.AddPlaylist("Playlist 2");

                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2")));
            }
        }

        [Fact]
        public async Task UpdateNowTriggersUpdate()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            using (var library = Helpers.CreateLibrary(fileSystem))
            {
                library.Initialize();

                var updated = library.IsUpdating.FirstAsync(x => x).PublishLast();
                updated.Connect();

                library.ChangeSongSourcePath("C://Test");

                library.UpdateNow();

                await updated.Timeout(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public void YoutubeDownloadPathSetterThrowsArgumentExceptionIfDirectoryDoesntExist()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.YoutubeDownloadPath = "C://Test");
            }
        }

        [Fact]
        public void YoutubeDownloadPathSmokeTest()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            using (Library library = Helpers.CreateLibrary(fileSystem))
            {
                library.YoutubeDownloadPath = "C://Test";
            }
        }
    }
}