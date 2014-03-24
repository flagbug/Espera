using Akavache;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Microsoft.Reactive.Testing;
using Moq;
using NSubstitute;
using ReactiveUI;
using ReactiveUI.Testing;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
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
        public void AddSongsToPlaylistThrowsAccessExceptionWithGuestTokenAndMultipleSongs()
        {
            var songs = new[]
            {
                Substitute.For<Song>("TestPath", TimeSpan.Zero),
                Substitute.For<Song>("TestPath", TimeSpan.Zero)
            };

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.LocalAccessControl.SetLocalPassword(token, "Password");
                library.LocalAccessControl.DowngradeLocalAccess(token);

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
        public async Task CanPlayAWholeBunchOfSongs()
        {
            var song = new LocalSong("C://", TimeSpan.Zero);
            var awaitSubject = new AsyncSubject<Unit>();
            int invocationCount = 0;

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AudioPlayerCallback.PlayRequest = () =>
                {
                    invocationCount++;

                    if (invocationCount == 100)
                    {
                        awaitSubject.OnNext(Unit.Default);
                        awaitSubject.OnCompleted();
                    }

                    Task.Run(() => library.AudioPlayerCallback.Finished());

                    return Task.Delay(0);
                };

                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                await library.PlayInstantlyAsync(Enumerable.Repeat(song, 100).ToList(), token);

                await awaitSubject.Timeout(TimeSpan.FromSeconds(5));
            }

            Assert.Equal(100, invocationCount);
        }

        [Fact]
        public async Task ChangeSongSourcePathSmokeTest()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            using (Library library = Helpers.CreateLibrary(fileSystem))
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
        public async Task ChangeSongSourcePathTriggersUpdate()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            using (var library = Helpers.CreateLibrary(fileSystem))
            {
                library.Initialize();

                var updated = library.IsUpdating.FirstAsync(x => x).PublishLast();
                updated.Connect();

                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.ChangeSongSourcePath("C://Test", token);

                await updated.Timeout(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public async Task ContinueSongCallsAudioPlayerPlay()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                bool called = false;
                library.AudioPlayerCallback.PlayRequest = () =>
                {
                    called = true;
                    return Task.Delay(0);
                };

                Mock<Song> song = Helpers.CreateSongMock();

                library.AddSongToPlaylist(song.Object);

                await library.PlaySongAsync(0, token);

                await library.ContinueSongAsync(token);

                Assert.True(called);
            }
        }

        [Fact]
        public async Task ContinueSongThrowsAccessExceptionWithGuestToken()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.LocalAccessControl.SetLocalPassword(token, "Password");
                library.LocalAccessControl.DowngradeLocalAccess(token);

                await Helpers.ThrowsAsync<AccessException>(async () => await library.ContinueSongAsync(token));
            }
        }

        [Fact]
        public void DisabledAutomaticUpdatesDoesntTriggerUpdate()
        {
            var fileSystem = Substitute.For<IFileSystem>();

            var settings = new CoreSettings
            {
                EnableAutomaticLibraryUpdates = false
            };

            using (Library library = Helpers.CreateLibrary(settings, null, null, fileSystem))
            {
                (new TestScheduler()).With(scheduler =>
                {
                    library.Initialize();

                    scheduler.AdvanceByMs(settings.SongSourceUpdateInterval.TotalMilliseconds);
                });
            }

            fileSystem.Directory.DidNotReceiveWithAnyArgs().GetFiles(null);
        }

        [Fact]
        public async Task ExternalTagChangesArePropagated()
        {
            var song = new LocalSong("C://Song.mp3", TimeSpan.Zero) { Title = "A" };
            var updatedSong = new LocalSong("C://Song.mp3", TimeSpan.Zero) { Title = "B" };

            var libraryReader = Substitute.For<ILibraryReader>();
            libraryReader.LibraryExists.Returns(true);
            libraryReader.ReadSongSourcePath().Returns("C://");
            libraryReader.ReadPlaylists().Returns(new List<Playlist>());
            libraryReader.ReadSongs().Returns(new[] { song });

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { song.OriginalPath, new MockFileData("DontCare") } });

            var songFinder = Substitute.For<ILocalSongFinder>();
            songFinder.GetSongsAsync().Returns(Observable.Return(Tuple.Create(updatedSong, (byte[])null)));

            using (Library library = Helpers.CreateLibrary(libraryReader, fileSystem, songFinder))
            {
                await library.AwaitInitializationAndUpdate();

                Assert.Equal("B", library.Songs[0].Title);
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
            var reader = Substitute.For<ILibraryReader>();
            reader.LibraryExists.Returns(true);
            reader.ReadPlaylists().Returns(new List<Playlist>());
            reader.ReadSongSourcePath().Returns(String.Empty);
            reader.ReadSongs().Returns(x =>
            {
                readerFired.OnNext(1);
                return new List<LocalSong>();
            });

            using (var library = Helpers.CreateLibrary(null, reader, null, fileSystem))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.ChangeSongSourcePath("C://Test", token);

                var isUpdating = library.IsUpdating.FirstAsync(x => x).Select(x => 2).PublishLast();
                isUpdating.Connect();

                var first = readerFired.Amb(isUpdating).FirstAsync().PublishLast();
                first.Connect();

                library.Initialize();

                Assert.Equal(1, await first.Timeout(TimeSpan.FromSeconds(5)));
            }
        }

        [Fact]
        public async Task IsUpdatingSmokeTest()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            using (var library = Helpers.CreateLibrary(fileSystem))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.ChangeSongSourcePath("C://Test", token);

                var isUpdating = library.IsUpdating.CreateCollection();

                var last = library.IsUpdating.Where(x => !x).ElementAt(1).PublishLast();
                last.Connect();

                library.Initialize();

                await last.Timeout(TimeSpan.FromSeconds(5));

                Assert.Equal(new[] { false, true, false }, isUpdating);
            }
        }

        [Fact]
        public async Task PauseSongCallsAudioPlayerPause()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                bool called = false;
                library.AudioPlayerCallback.PauseRequest = () =>
                {
                    called = true;
                    return Task.Delay(0);
                };

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
                library.LocalAccessControl.SetLocalPassword(token, "Password");
                library.LocalAccessControl.DowngradeLocalAccess(token);

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
                Guid token = library.RemoteAccessControl.RegisterRemoteAccessToken(new Guid());

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
                var conn = library.PlaybackState.Where(x => x == AudioPlayerState.Playing)
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
                library.AudioPlayerCallback.PlayRequest = () =>
                {
                    called++;
                    return Task.Delay(0);
                };
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

                        return Task.Delay(0);
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
                library.LocalAccessControl.SetLocalPassword(token, "Password");
                library.LocalAccessControl.DowngradeLocalAccess(token);

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

                var coll = library.PlaybackState.CreateCollection();

                var conn = library.PlaybackState.Where(x => x == AudioPlayerState.Playing)
                    .Take(2)
                    .PublishLast();
                conn.Connect();

                await library.PlaySongAsync(0, token);

                await conn.Timeout(TimeSpan.FromSeconds(5));
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
                library.LocalAccessControl.SetLocalPassword(token, "Password");
                library.LocalAccessControl.DowngradeLocalAccess(token);

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
            var songMock = Substitute.For<Song>("TestPath", TimeSpan.Zero);

            var settings = new CoreSettings
            {
                LockPlaylist = true
            };

            using (Library library = Helpers.CreateLibraryWithPlaylist(settings: settings))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                library.LocalAccessControl.SetLocalPassword(token, "Password");
                library.LocalAccessControl.DowngradeLocalAccess(token);

                library.AddSongToPlaylist(songMock);

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

                library.AudioPlayerCallback.StopRequest = () =>
                {
                    finishedFired = true;
                    return Task.Delay(0);
                };
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
        public async Task RemovesArtworkOnlyWithoutReferenceToSong()
        {
            var existingSong = new LocalSong("C://Existing.mp3", TimeSpan.Zero, "artwork-abcdefg");
            var missingSong = new LocalSong("C://Missing.mp3", TimeSpan.Zero, "artwork-abcdefg");
            await BlobCache.LocalMachine.Insert("artwork-abcdefg", new byte[] { 0, 1 });

            var libraryReader = Substitute.For<ILibraryReader>();
            libraryReader.LibraryExists.Returns(true);
            libraryReader.ReadSongSourcePath().Returns("C://");
            libraryReader.ReadPlaylists().Returns(new List<Playlist>());
            libraryReader.ReadSongs().Returns(new[] { existingSong, missingSong });

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { existingSong.OriginalPath, new MockFileData("DontCare") } });

            using (Library library = Helpers.CreateLibrary(libraryReader, fileSystem))
            {
                await library.AwaitInitializationAndUpdate();

                Assert.NotNull(BlobCache.LocalMachine.GetAllKeys().FirstOrDefault(x => x == "artwork-abcdefg"));
            }
        }

        [Fact]
        public async Task RemovesArtworkWhenSongIsMissing()
        {
            var missingSong = new LocalSong("C://Missing.mp3", TimeSpan.Zero, "artwork-abcdefg");
            await BlobCache.LocalMachine.Insert("artwork-abcdefg", new byte[] { 0, 1 });

            var libraryReader = Substitute.For<ILibraryReader>();
            libraryReader.LibraryExists.Returns(true);
            libraryReader.ReadSongSourcePath().Returns("C://");
            libraryReader.ReadPlaylists().Returns(new List<Playlist>());
            libraryReader.ReadSongs().Returns(new[] { missingSong });

            using (Library library = Helpers.CreateLibrary(libraryReader))
            {
                await library.AwaitInitializationAndUpdate();

                Assert.Null(BlobCache.LocalMachine.GetAllKeys().FirstOrDefault(x => x == "artwork-abcdefg"));
            }
        }

        [Fact]
        public async Task RemovesMissingSongsFromLibrary()
        {
            var existingSong = new LocalSong("C://Existing.mp3", TimeSpan.Zero);
            var missingSong = new LocalSong("C://Missing.mp3", TimeSpan.Zero);

            var libraryReader = Substitute.For<ILibraryReader>();
            libraryReader.LibraryExists.Returns(true);
            libraryReader.ReadSongSourcePath().Returns("C://");
            libraryReader.ReadPlaylists().Returns(new List<Playlist>());
            libraryReader.ReadSongs().Returns(new[] { existingSong, missingSong });

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { existingSong.OriginalPath, new MockFileData("DontCare") } });

            using (Library library = Helpers.CreateLibrary(libraryReader, fileSystem))
            {
                await library.AwaitInitializationAndUpdate();

                Assert.Equal(1, library.Songs.Count());
            }
        }

        [Fact]
        public async Task RemovesMissingSongsFromPlaylists()
        {
            var existingSong = new LocalSong("C://Existing.mp3", TimeSpan.Zero);
            var missingSong = new LocalSong("C://Missing.mp3", TimeSpan.Zero);

            var songs = new[] { existingSong, missingSong };

            var playlist1 = new Playlist("Playlist 1");
            playlist1.AddSongs(songs);

            var playlist2 = new Playlist("Playlist 2");
            playlist2.AddSongs(songs);

            var libraryReader = Substitute.For<ILibraryReader>();
            libraryReader.LibraryExists.Returns(true);
            libraryReader.ReadSongSourcePath().Returns("C://");
            libraryReader.ReadPlaylists().Returns(new[] { playlist1, playlist2 });
            libraryReader.ReadSongs().Returns(songs);

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { existingSong.OriginalPath, new MockFileData("DontCare") } });

            using (Library library = Helpers.CreateLibrary(libraryReader, fileSystem))
            {
                await library.AwaitInitializationAndUpdate();

                Assert.Equal(1, library.Playlists[0].Count());
                Assert.Equal(1, library.Playlists[1].Count());
            }
        }

        [Fact]
        public async Task RemovesMissingSongWithoutArtworkFromLibraryWhenOtherArtworksArePresent()
        {
            var existingSong = new LocalSong("C://Existing.mp3", TimeSpan.Zero, "artwork-abcdefg");
            var missingSong = new LocalSong("C://Missing.mp3", TimeSpan.Zero);

            var libraryReader = Substitute.For<ILibraryReader>();
            libraryReader.LibraryExists.Returns(true);
            libraryReader.ReadSongSourcePath().Returns("C://");
            libraryReader.ReadPlaylists().Returns(new List<Playlist>());
            libraryReader.ReadSongs().Returns(new[] { existingSong, missingSong });

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { existingSong.OriginalPath, new MockFileData("DontCare") } });

            using (Library library = Helpers.CreateLibrary(libraryReader, fileSystem))
            {
                await library.AwaitInitializationAndUpdate();

                Assert.Equal(1, library.Songs.Count());
            }
        }

        [Fact]
        public async Task SaveDoesNotSaveTemporaryPlaylist()
        {
            var libraryWriter = Substitute.For<ILibraryWriter>();

            using (Library library = Helpers.CreateLibrary(libraryWriter))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddAndSwitchToPlaylist("Playlist", token);

                await library.PlayInstantlyAsync(Helpers.SetupSongMocks(1), token);

                library.Save();
            }

            libraryWriter.Received(1).Write(Arg.Any<IEnumerable<LocalSong>>(), Arg.Is<IEnumerable<Playlist>>(x => x.Count() == 1), Arg.Any<string>());
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

                    return Task.Delay(0);
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
                LockPlaylist = true
            };

            using (Library library = Helpers.CreateLibraryWithPlaylist("Playlist 1", settings))
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.AddPlaylist("Playlist 2", token);

                library.LocalAccessControl.SetLocalPassword(token, "Password");
                library.LocalAccessControl.DowngradeLocalAccess(token);

                Assert.Throws<AccessException>(() => library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2"), token));
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

                var firstUpdateFinished = library.IsUpdating.Where(x => !x).ElementAt(1).PublishLast();
                firstUpdateFinished.Connect();

                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                library.ChangeSongSourcePath("C://Test", token);

                await firstUpdateFinished.Timeout(TimeSpan.FromSeconds(5));

                var updated = library.IsUpdating.FirstAsync(x => x).PublishLast();
                updated.Connect();

                library.UpdateNow();

                await updated.Timeout(TimeSpan.FromSeconds(5));
            }
        }

        [Fact]
        public void YoutubeDownloadPathSetterThrowsArgumentExceptionIfDirectoryDoesntExist()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.SwitchToPlaylist(null, library.LocalAccessControl.RegisterLocalAccessToken()));
            }
        }
    }
}