using Akavache;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Microsoft.Reactive.Testing;
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
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public sealed class LibraryTest
    {
        [Fact]
        public async Task CanPlayAWholeBunchOfSongs()
        {
            var song = new LocalSong("C://", TimeSpan.Zero);

            using (Library library = new LibraryBuilder().WithPlaylist().Build())
            {
                var awaiter = library.PlaybackState.Where(x => x == AudioPlayerState.Playing)
                    .Select((x, i) => i + 1)
                    .FirstAsync(x => x == 10)
                    .PublishLast();
                awaiter.Connect();

                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                await library.PlayInstantlyAsync(Enumerable.Repeat(song, 10).ToList(), token);

                await awaiter.Timeout(TimeSpan.FromSeconds(5));
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

        public class TheAddAndSwitchToPlaylistMethod
        {
            [Fact]
            public void SmokeTest()
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
            public void ThrowsInvalidOperationExceptionIfPlaylistWithExistingNameIsAdded()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.AddAndSwitchToPlaylist("Playlist", token);

                    Assert.Throws<InvalidOperationException>(() => library.AddAndSwitchToPlaylist("Playlist", token));
                }
            }
        }

        public class TheAddGuestSongToPlaylistMethod
        {
            [Fact]
            public void ThrowsArgumentNullExceptionIfSongIsNull()
            {
                using (Library library = new LibraryBuilder().WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(accessToken, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    Assert.Throws<ArgumentNullException>(() => library.AddGuestSongToPlaylist(null, accessToken));
                }
            }

            [Fact]
            public void ThrowsInvalidOperationExceptionIfAccessTokenIsNotGuestToken()
            {
                var settings = new CoreSettings { EnableGuestSystem = false };

                using (Library library = new LibraryBuilder().WithSettings(settings).WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    Assert.Throws<InvalidOperationException>(() => library.AddGuestSongToPlaylist(Helpers.SetupSongMock(), accessToken));
                }
            }

            [Fact]
            public void ThrowsInvalidOperationExceptionIfGuestSystemIsDisabled()
            {
                var settings = new CoreSettings { EnableGuestSystem = false };

                using (Library library = new LibraryBuilder().WithSettings(settings).WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(accessToken, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    Assert.Throws<InvalidOperationException>(() => library.AddGuestSongToPlaylist(Helpers.SetupSongMock(), accessToken));
                }
            }
        }

        public class TheAddPlaylistMethod
        {
            [Fact]
            public void ThrowInvalidOperationExceptionIfPlaylistWithExistingNameIsAdded()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.AddPlaylist("Playlist", token);

                    Assert.Throws<InvalidOperationException>(() => library.AddPlaylist("Playlist", token));
                }
            }

            [Fact]
            public void ThrowsArgumentNullExceptionIfNameIsNull()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentNullException>(() => library.AddPlaylist(null, library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }
        }

        public class TheAddSongsToPlaylistMethod
        {
            [Fact]
            public void RequiresAdminPermission()
            {
                var songs = new[]
                {
                    Substitute.For<Song>("TestPath", TimeSpan.Zero)
                };

                using (Library library = new LibraryBuilder().WithPlaylist().Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(token, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(token);

                    Assert.Throws<AccessException>(() => library.AddSongsToPlaylist(songs, token));
                }
            }

            [Fact]
            public void ThrowsArgumentNullExceptionIfSongListIsNull()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentNullException>(() => library.AddSongsToPlaylist(null, library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }
        }

        public class TheChangeSongSourceMethod
        {
            [Fact]
            public void SmokeTest()
            {
                var fileSystem = new MockFileSystem();
                fileSystem.Directory.CreateDirectory("C://Test");

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).Build())
                {
                    library.ChangeSongSourcePath("C://Test", library.LocalAccessControl.RegisterLocalAccessToken());
                    Assert.Equal("C://Test", library.SongSourcePath);
                }
            }

            [Fact]
            public void ThrowsInvalidOperationExceptionIfDirectoryDoesntExist()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Assert.Throws<InvalidOperationException>(() => library.ChangeSongSourcePath("C://Test", library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }

            [Fact]
            public async Task TriggersUpdate()
            {
                var fileSystem = new MockFileSystem();
                fileSystem.Directory.CreateDirectory("C://Test");

                using (var library = new LibraryBuilder().WithFileSystem(fileSystem).Build())
                {
                    library.Initialize();

                    var updated = library.WhenAnyValue(x => x.IsUpdating).FirstAsync(x => x).PublishLast();
                    updated.Connect();

                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.ChangeSongSourcePath("C://Test", token);

                    await updated.Timeout(TimeSpan.FromSeconds(5));
                }
            }
        }

        public class TheContinueSongAsyncMethod
        {
            [Fact]
            public async Task CallsAudioPlayerPlay()
            {
                var audioPlayerCallback = Substitute.For<IMediaPlayerCallback>();

                using (Library library = new LibraryBuilder().WithPlaylist().WithAudioPlayer(audioPlayerCallback).Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    Song song = Helpers.SetupSongMock();

                    library.AddSongsToPlaylist(new[] { song }, token);

                    await library.PlaySongAsync(0, token);

                    await library.ContinueSongAsync(token);
                }

                audioPlayerCallback.Received(2).PlayAsync();
            }

            [Fact]
            public async Task ThrowsAccessExceptionWithGuestToken()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.LocalAccessControl.SetLocalPassword(token, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(token);

                    await Helpers.ThrowsAsync<AccessException>(async () => await library.ContinueSongAsync(token));
                }
            }
        }

        public class TheGetPlaylistByNameMethod
        {
            [Fact]
            public void ReturnsNullIfPlaylistDoesNotExist()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Assert.Null(library.GetPlaylistByName("Playlist"));
                }
            }

            [Fact]
            public void ThrowsArgumentNullExceptionIfPlaylistNameIsNull()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentNullException>(() => library.GetPlaylistByName(null));
                }
            }
        }

        public class TheInitializeMethod
        {
            [Fact]
            public async Task FiresInitalUpdateAfterLibraryLoad()
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

                    var isUpdating = library.WhenAnyValue(x => x.IsUpdating).FirstAsync(x => x).Select(x => 2).PublishLast();
                    isUpdating.Connect();

                    var first = readerFired.Amb(isUpdating).FirstAsync().PublishLast();
                    first.Connect();

                    library.Initialize();

                    Assert.Equal(1, await first.Timeout(TimeSpan.FromSeconds(5)));
                }
            }

            [Fact]
            public void RetriesLibraryLoadThreeTimesBeforeGivingUp()
            {
                var reader = Substitute.For<ILibraryReader>();
                reader.LibraryExists.Returns(true);
                reader.ReadSongs().Returns(_ => { throw new LibraryReadException("Yadda", null); });

                using (Library library = new LibraryBuilder().WithReader(reader).Build())
                {
                    library.Initialize();
                }

                reader.Received(3).ReadSongs();
            }
        }

        public class TheIsUpdatingProperty
        {
            [Fact]
            public async Task SmokeTest()
            {
                var fileSystem = new MockFileSystem();
                fileSystem.Directory.CreateDirectory("C://Test");

                using (var library = new LibraryBuilder().WithFileSystem(fileSystem).Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.ChangeSongSourcePath("C://Test", token);

                    var isUpdating = library.WhenAnyValue(x => x.IsUpdating).CreateCollection();

                    var last = library.WhenAnyValue(x => x.IsUpdating).Where(x => !x).ElementAt(1).PublishLast();
                    last.Connect();

                    await library.AwaitInitializationAndUpdate();

                    Assert.Equal(new[] { false, true, false }, isUpdating);
                }
            }
        }

        public class ThePauseSongAsyncMethod
        {
            [Fact]
            public async Task CallsAudioPlayerPause()
            {
                var audioPlayerCallback = Substitute.For<IMediaPlayerCallback>();

                using (Library library = new LibraryBuilder().WithPlaylist().WithAudioPlayer(audioPlayerCallback).Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    Song song = Helpers.SetupSongMock();

                    library.AddSongsToPlaylist(new[] { song }, token);

                    await library.PlaySongAsync(0, token);

                    await library.PauseSongAsync(token);
                }

                audioPlayerCallback.Received(1).PauseAsync();
            }

            [Fact]
            public async Task ThrowsAccessExceptionIfIsNotAdministratorAndPausingIsLocked()
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
        }

        public class ThePlayInstantlyAsyncMethod
        {
            [Fact]
            public async Task JumpsOverCorruptedSong()
            {
                using (var handle = new CountdownEvent(2))
                {
                    var audioPlayer = Substitute.For<IMediaPlayerCallback>();
                    audioPlayer.PlayAsync().Returns(_ => Task.Run(() =>
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
                    }));

                    using (Library library = new LibraryBuilder().WithPlaylist().WithAudioPlayer(audioPlayer).Build())
                    {
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
            public async Task PlaysMultipleSongsInARow()
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
            public async Task SmokeTest()
            {
                var audioPlayer = Substitute.For<IMediaPlayerCallback>();

                using (Library library = new LibraryBuilder().WithAudioPlayer(audioPlayer).Build())
                {
                    Song song = Helpers.SetupSongMock();

                    await library.PlayInstantlyAsync(new[] { song }, library.LocalAccessControl.RegisterLocalAccessToken());
                }

                audioPlayer.Received(1).PlayAsync();
            }

            [Fact]
            public async Task ThrowsArgumentNullExceptionIfSongListIsNull()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    await Helpers.ThrowsAsync<ArgumentNullException>(async () => await library.PlayInstantlyAsync(null, library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }

            [Fact]
            public async Task ValidatesRemoteAccess()
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
        }

        public class ThePlayNextSongAsyncMethod
        {
            [Fact]
            public async Task ThrowsAccessExceptionIfUserIsNotAdministrator()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(token, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(token);

                    await Helpers.ThrowsAsync<AccessException>(async () => await library.PlayNextSongAsync(token));
                }
            }
        }

        public class ThePlayPreviousSongAsyncMethod
        {
            [Fact]
            public async Task ThrowsInvalidOperationExceptionIfPlaylistIsEmpty()
            {
                using (Library library = new LibraryBuilder().WithPlaylist().Build())
                {
                    await Helpers.ThrowsAsync<InvalidOperationException>(async () => await library.PlayPreviousSongAsync(library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }
        }

        public class ThePlaySongAsyncMethod
        {
            [Fact]
            public async Task PlaysNextSongAutomatically()
            {
                using (Library library = new LibraryBuilder().WithPlaylist().Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.AddSongsToPlaylist(Helpers.SetupSongMocks(2), token);

                    var conn = library.PlaybackState.Where(x => x == AudioPlayerState.Playing)
                        .Take(2)
                        .PublishLast();
                    conn.Connect();

                    await library.PlaySongAsync(0, token);

                    await conn.Timeout(TimeSpan.FromSeconds(5));
                }
            }

            [Fact]
            public async Task ResetsSongIsCorruptedIfPlayingIsWorking()
            {
                var audioPlayerCallback = Substitute.For<IMediaPlayerCallback>();
                audioPlayerCallback.LoadAsync(Arg.Any<Uri>()).Returns(Observable.Throw<Unit>(new SongLoadException()).ToTask());

                using (Library library = new LibraryBuilder().WithPlaylist().WithAudioPlayer(audioPlayerCallback).Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    Song song = Helpers.SetupSongMock();

                    library.AddSongsToPlaylist(new[] { song }, accessToken);

                    await library.PlaySongAsync(0, accessToken);

                    audioPlayerCallback.LoadAsync(Arg.Any<Uri>()).Returns(_ => Task.Delay(0));

                    await library.PlaySongAsync(0, accessToken);

                    Assert.False(song.IsCorrupted);
                }
            }

            [Fact]
            public async Task SetsSongIsCorruptedToTrueIfLoadIsFailing()
            {
                var audioPlayerCallback = Substitute.For<IMediaPlayerCallback>();
                audioPlayerCallback.LoadAsync(Arg.Any<Uri>()).Returns(Observable.Throw<Unit>(new SongLoadException()).ToTask());

                using (Library library = new LibraryBuilder().WithPlaylist().WithAudioPlayer(audioPlayerCallback).Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    Song song = Helpers.SetupSongMock();

                    library.AddSongsToPlaylist(new[] { song }, accessToken);

                    await library.PlaySongAsync(0, accessToken);

                    Assert.True(song.IsCorrupted);
                }
            }

            [Fact]
            public async Task SetsSongIsCorruptedToTrueIfPlayIsFailing()
            {
                var audioPlayerCallback = Substitute.For<IMediaPlayerCallback>();
                audioPlayerCallback.PlayAsync().Returns(Observable.Throw<Unit>(new PlaybackException()).ToTask());

                using (Library library = new LibraryBuilder().WithPlaylist().WithAudioPlayer(audioPlayerCallback).Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    Song song = Helpers.SetupSongMock();

                    library.AddSongsToPlaylist(new[] { song }, accessToken);

                    await library.PlaySongAsync(0, accessToken);

                    Assert.True(song.IsCorrupted);
                }
            }

            [Fact]
            public async Task SongPreparationCanTimeout()
            {
                await new TestScheduler().With(async sched =>
                {
                    var song = Substitute.For<Song>("C://", TimeSpan.Zero);
                    song.PrepareAsync(Arg.Any<YoutubeStreamingQuality>()).Returns(Observable.Return(Unit.Default)
                        .Delay(Library.PreparationTimeout + TimeSpan.FromSeconds(1), sched).ToTask());

                    var audioPlayerCallback = Substitute.For<IMediaPlayerCallback>();

                    using (Library library = new LibraryBuilder().WithPlaylist().Build())
                    {
                        Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                        library.AddSongsToPlaylist(new[] { song }, token);

                        Task play = library.PlaySongAsync(0, token);

                        sched.AdvanceByMs((Library.PreparationTimeout + TimeSpan.FromSeconds(2)).TotalMilliseconds);

                        await play;
                    }

                    audioPlayerCallback.DidNotReceiveWithAnyArgs().LoadAsync(Arg.Any<Uri>());
                });
            }

            [Fact]
            public async Task ThrowsAccessExceptionIfUserIsNotAdministratorAndLockPlayPauseIsTrue()
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
            public async Task ThrowsArgumentOutOfRangeExceptionIfIndexIsLessThanZero()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    await Helpers.ThrowsAsync<ArgumentOutOfRangeException>(async () => await library.PlaySongAsync(-1, library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }
        }

        public class TheRemoveFromPlaylistMethod
        {
            [Fact]
            public void ByIndexesTest()
            {
                using (Library library = new LibraryBuilder().WithPlaylist().Build())
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
            public void BySongReferenceTest()
            {
                using (Library library = new LibraryBuilder().WithPlaylist().Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    Song[] songs = Helpers.SetupSongMocks(4);

                    library.AddSongsToPlaylist(songs, token);

                    library.RemoveFromPlaylist(new[] { songs[0], songs[2] }, token);

                    Song[] remaining = library.CurrentPlaylist.Select(entry => entry.Song).ToArray();

                    Assert.Equal(songs[1], remaining[0]);
                    Assert.Equal(songs[3], remaining[1]);
                }
            }

            [Fact]
            public void SmokeTest()
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
            public void ThrowsAccessExceptionIfAccessModeIsPartyAndLockPlaylistRemovalIsTrue()
            {
                var songMock = Substitute.For<Song>("TestPath", TimeSpan.Zero);

                var settings = new CoreSettings
                {
                    LockPlaylist = true
                };

                using (Library library = new LibraryBuilder().WithPlaylist().WithSettings(settings).Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.AddSongsToPlaylist(new[] { songMock }, token);

                    library.LocalAccessControl.SetLocalPassword(token, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(token);

                    Assert.Throws<AccessException>(() => library.RemoveFromPlaylist(new[] { 0 }, token));
                }
            }

            [Fact]
            public void ThrowsArgumentNullExceptionIfIndexesIsNull()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<int>)null, library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }

            [Fact]
            public void ThrowsArgumentNullExceptionIfPlaylistNameIsNull()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentNullException>(() => library.RemovePlaylist(null, library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }

            [Fact]
            public void ThrowsArgumentNullExceptionIfSongListIsNull()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<Song>)null, library.LocalAccessControl.RegisterLocalAccessToken()));
                }
            }

            [Fact]
            public async Task WhileSongIsPlayingStopsCurrentSong()
            {
                using (Library library = new LibraryBuilder().WithPlaylist().Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.AddSongsToPlaylist(Helpers.SetupSongMocks(1), token);

                    var awaiter = library.PlaybackState.FirstAsync(x => x == AudioPlayerState.Finished).PublishLast();
                    awaiter.Connect();

                    await library.PlaySongAsync(0, token);

                    library.RemoveFromPlaylist(new[] { 0 }, token);

                    await awaiter.Timeout(TimeSpan.FromSeconds(5));
                }
            }
        }

        public class TheSaveMethod
        {
            [Fact]
            public async Task DoesNotSaveTemporaryPlaylist()
            {
                var libraryWriter = Substitute.For<ILibraryWriter>();

                using (Library library = new LibraryBuilder().WithWriter(libraryWriter).Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.AddAndSwitchToPlaylist("Playlist", token);

                    await library.PlayInstantlyAsync(Helpers.SetupSongMocks(1), token);

                    library.Save();
                }

                libraryWriter.Received(1).Write(Arg.Any<IEnumerable<LocalSong>>(), Arg.Is<IEnumerable<Playlist>>(x => x.Count() == 1), Arg.Any<string>());
            }

            [Fact]
            public void RetriesThreeTimesBeforeGivingUp()
            {
                var libraryWriter = Substitute.For<ILibraryWriter>();
                libraryWriter.When(x => x.Write(Arg.Any<IEnumerable<LocalSong>>(), Arg.Any<IEnumerable<Playlist>>(), Arg.Any<string>()))
                    .Do(x => { throw new LibraryWriteException("Yadda", null); });

                using (Library library = new LibraryBuilder().WithWriter(libraryWriter).Build())
                {
                    library.Save();
                }

                libraryWriter.ReceivedWithAnyArgs(3).Write(Arg.Any<IEnumerable<LocalSong>>(), Arg.Any<IEnumerable<Playlist>>(), Arg.Any<string>());
            }
        }

        public class TheSetCurrentTimeMethod
        {
            [Fact]
            public async Task PropagatesToMediaPlayer()
            {
                var audioPlayer = Substitute.For<IMediaPlayerCallback>();
                var timeSpan = TimeSpan.FromMinutes(1);

                using (Library library = new LibraryBuilder().WithAudioPlayer(audioPlayer).WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    await library.PlayInstantlyAsync(Helpers.SetupSongMocks(1), accessToken);

                    library.SetCurrentTime(timeSpan, accessToken);
                }

                Assert.Equal(timeSpan, audioPlayer.CurrentTime);
            }

            [Fact]
            public void VerifiesAccessRights()
            {
                var settings = new CoreSettings
                {
                    LockTime = false
                };

                using (Library library = new LibraryBuilder().WithSettings(settings).Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(accessToken, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    library.SetCurrentTime(TimeSpan.FromMinutes(1), accessToken);

                    library.LocalAccessControl.UpgradeLocalAccess(accessToken, "Password");
                    settings.LockTime = true;
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    Assert.Throws<AccessException>(() => library.SetCurrentTime(TimeSpan.FromMinutes(1), accessToken));
                }
            }
        }

        public class TheSetVolumeMethod
        {
            [Fact]
            public async Task PropagatesToMediaPlayer()
            {
                var audioPlayer = Substitute.For<IMediaPlayerCallback>();

                using (Library library = new LibraryBuilder().WithAudioPlayer(audioPlayer).WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    await library.PlayInstantlyAsync(Helpers.SetupSongMocks(1), accessToken);

                    library.SetVolume(0.5f, accessToken);
                }

                audioPlayer.Received().SetVolume(0.5f);
            }

            [Fact]
            public void PropagatesToSettings()
            {
                var settings = new CoreSettings
                {
                    Volume = 0.0f
                };

                using (Library library = new LibraryBuilder().WithSettings(settings).Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.SetVolume(0.5f, accessToken);
                }

                Assert.Equal(0.5f, settings.Volume);
            }

            [Fact]
            public void VerifiesAccessRights()
            {
                var settings = new CoreSettings
                {
                    LockVolume = false
                };

                using (Library library = new LibraryBuilder().WithSettings(settings).Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(accessToken, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    library.SetVolume(0.5f, accessToken);

                    library.LocalAccessControl.UpgradeLocalAccess(accessToken, "Password");
                    settings.LockVolume = true;
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    Assert.Throws<AccessException>(() => library.SetVolume(0.5f, accessToken));
                }
            }
        }

        public class TheShufflePlaylistMethod
        {
            [Fact]
            public void VerifiesAccessRights()
            {
                var settings = new CoreSettings
                {
                    LockPlaylist = false
                };

                using (Library library = new LibraryBuilder().WithSettings(settings).WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.LocalAccessControl.SetLocalPassword(accessToken, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    library.ShufflePlaylist(accessToken);

                    library.LocalAccessControl.UpgradeLocalAccess(accessToken, "Password");
                    settings.LockPlaylist = true;
                    library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                    Assert.Throws<AccessException>(() => library.ShufflePlaylist(accessToken));
                }
            }
        }

        public class TheSwitchToPlaylistMethod
        {
            [Fact]
            public async Task PreventsNextSongFromPlaying()
            {
                var audioPlayerCallback = Substitute.For<IMediaPlayerCallback>();

                using (Library library = new LibraryBuilder().WithPlaylist().WithAudioPlayer(audioPlayerCallback).Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    int played = 0;

                    audioPlayerCallback.PlayAsync().Returns(Task.Run(() =>
                    {
                        if (played == 0)
                        {
                            library.AddAndSwitchToPlaylist("Playlist2", token);
                        }

                        played++;
                    }));

                    library.AddSongsToPlaylist(Helpers.SetupSongMocks(2), token);

                    await library.PlaySongAsync(0, token);

                    Assert.Equal(1, played);
                }
            }

            [Fact(Skip = "Different outcome when running alone or in a group")]
            public async Task SetsCurrentSongIndexIfChangingToOtherPlaylistAndPlayingFirstSong()
            {
                using (Library library = new LibraryBuilder().WithPlaylist().Build())
                {
                    var coll = library.WhenAnyValue(x => x.CurrentPlaylist.CurrentSongIndex).CreateCollection();

                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.AddSongsToPlaylist(new[] { Helpers.SetupSongMock() }, token);

                    await library.PlaySongAsync(0, token);

                    library.AddPlaylist("Playlist 2", token);
                    library.SwitchToPlaylist(library.Playlists.Last(), token);
                    library.AddSongsToPlaylist(new[] { Helpers.SetupSongMock() }, token);

                    await library.PlaySongAsync(0, token);

                    Assert.Equal(new int?[] { null, 0, null, null, 0, null }, coll);
                }
            }

            [Fact]
            public void ThrowsAccessExceptionIfPartyModeAndLockPlaylistSwitchingIsTrue()
            {
                var settings = new CoreSettings
                {
                    LockPlaylist = true
                };

                using (Library library = new LibraryBuilder().WithPlaylist("Playlist 1").WithSettings(settings).Build())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.AddPlaylist("Playlist 2", token);

                    library.LocalAccessControl.SetLocalPassword(token, "Password");
                    library.LocalAccessControl.DowngradeLocalAccess(token);

                    Assert.Throws<AccessException>(() => library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2"), token));
                }
            }
        }

        public class TheUpdateBehavior
        {
            [Fact]
            public void DisabledAutomaticUpdatesDoesntTriggerUpdate()
            {
                var settings = new CoreSettings
                {
                    EnableAutomaticLibraryUpdates = false
                };

                var libraryReader = Substitute.For<ILibraryReader>();
                libraryReader.LibraryExists.Returns(true);
                libraryReader.ReadSongSourcePath().Returns("C://");
                libraryReader.ReadPlaylists().Returns(new List<Playlist>());
                libraryReader.ReadSongs().Returns(new List<LocalSong>());

                var songFinder = Substitute.For<ILocalSongFinder>();
                songFinder.GetSongsAsync().Returns(Observable.Empty<Tuple<LocalSong, byte[]>>());

                var fileSystem = new MockFileSystem();
                fileSystem.Directory.CreateDirectory("C://");

                using (Library library = new LibraryBuilder().WithSettings(settings).WithReader(libraryReader).WithFileSystem(fileSystem).WithSongFinder(songFinder).Build())
                {
                    (new TestScheduler()).With(scheduler =>
                    {
                        library.Initialize();

                        scheduler.AdvanceByMs(settings.SongSourceUpdateInterval.TotalMilliseconds + 1);
                    });
                }

                songFinder.DidNotReceive().GetSongsAsync();
            }

            [Fact]
            public async Task ExternalTagChangesArePropagated()
            {
                var artworkKey = BlobCacheKeys.GetKeyForArtwork(new byte[] { 0, 1 });
                var song = new LocalSong("C://Song.mp3", TimeSpan.Zero, artworkKey)
                {
                    Album = "Album-A",
                    Artist = "Artist-A",
                    Title = "Title-A",
                    Genre = "Genre-A"
                };

                byte[] updatedArtworkData = { 1, 0 };
                var updatedSong = new LocalSong("C://Song.mp3", TimeSpan.Zero)
                {
                    Album = "Album-B",
                    Artist = "Artist-B",
                    Title = "Title-B",
                    Genre = "Genre-B"
                };

                var libraryReader = Substitute.For<ILibraryReader>();
                libraryReader.LibraryExists.Returns(true);
                libraryReader.ReadSongSourcePath().Returns("C://");
                libraryReader.ReadPlaylists().Returns(new List<Playlist>());
                libraryReader.ReadSongs().Returns(new[] { song });

                var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { song.OriginalPath, new MockFileData("DontCare") } });

                var songFinder = Substitute.For<ILocalSongFinder>();
                songFinder.GetSongsAsync().Returns(Observable.Return(Tuple.Create(updatedSong, updatedArtworkData)));

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithReader(libraryReader).WithSongFinder(songFinder).Build())
                {
                    await library.AwaitInitializationAndUpdate();

                    Assert.True(ReferenceEquals(song, library.Songs[0]));

                    Assert.Equal(updatedSong.Album, song.Album);
                    Assert.Equal(updatedSong.Artist, song.Artist);
                    Assert.Equal(BlobCacheKeys.GetKeyForArtwork(updatedArtworkData), song.ArtworkKey);
                    Assert.Equal(updatedSong.Genre, song.Genre);
                    Assert.Equal(updatedSong.Title, song.Title);
                }
            }

            [Fact]
            public async Task InvokesSongsUpdatedObservableWhenSongAdded()
            {
                var song = new LocalSong("C://Song.mp3", TimeSpan.Zero);

                var libraryReader = Substitute.For<ILibraryReader>();
                libraryReader.LibraryExists.Returns(true);
                libraryReader.ReadSongSourcePath().Returns("C://");
                libraryReader.ReadPlaylists().Returns(new List<Playlist>());
                libraryReader.ReadSongs().Returns(new List<LocalSong>());

                var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { song.OriginalPath, new MockFileData("DontCare") } });

                var songFinder = Substitute.For<ILocalSongFinder>();
                songFinder.GetSongsAsync().Returns(Observable.Return(Tuple.Create(song, (byte[])null)));

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithReader(libraryReader).WithSongFinder(songFinder).Build())
                {
                    var update = library.SongsUpdated.CreateCollection();

                    await library.AwaitInitializationAndUpdate();

                    Assert.Equal(1, update.Count);
                }
            }

            [Fact]
            public async Task InvokesSongsUpdatedObservableWhenSongMetadataChanged()
            {
                var artworkKey = BlobCacheKeys.GetKeyForArtwork(new byte[] { 0, 1 });
                var song = new LocalSong("C://Song.mp3", TimeSpan.Zero, artworkKey)
                {
                    Album = "Album-A",
                    Artist = "Artist-A",
                    Title = "Title-A",
                    Genre = "Genre-A"
                };

                byte[] updatedArtworkData = { 1, 0 };
                var updatedSong = new LocalSong("C://Song.mp3", TimeSpan.Zero)
                {
                    Album = "Album-B",
                    Artist = "Artist-B",
                    Title = "Title-B",
                    Genre = "Genre-B"
                };

                var libraryReader = Substitute.For<ILibraryReader>();
                libraryReader.LibraryExists.Returns(true);
                libraryReader.ReadSongSourcePath().Returns("C://");
                libraryReader.ReadPlaylists().Returns(new List<Playlist>());
                libraryReader.ReadSongs().Returns(new[] { song });

                var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { song.OriginalPath, new MockFileData("DontCare") } });

                var songFinder = Substitute.For<ILocalSongFinder>();
                songFinder.GetSongsAsync().Returns(Observable.Return(Tuple.Create(updatedSong, updatedArtworkData)));

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithReader(libraryReader).WithSongFinder(songFinder).Build())
                {
                    var update = library.SongsUpdated.CreateCollection();

                    await library.AwaitInitializationAndUpdate();

                    Assert.Equal(1, update.Count); ;
                }
            }

            [Fact]
            public async Task UnavailableSongsSourceDoesntPurgeLibrary()
            {
                var settings = new CoreSettings
                {
                    EnableAutomaticLibraryUpdates = false
                };

                var song = new LocalSong("C://Song.mp3", TimeSpan.Zero) { Title = "A" };

                var libraryReader = Substitute.For<ILibraryReader>();
                libraryReader.LibraryExists.Returns(true);
                libraryReader.ReadSongSourcePath().Returns("C://");
                libraryReader.ReadPlaylists().Returns(new List<Playlist>());
                libraryReader.ReadSongs().Returns(new[] { song });

                var fileSystem = new MockFileSystem();

                using (Library library = Helpers.CreateLibrary(settings, libraryReader, fileSystem: fileSystem))
                {
                    library.Initialize();

                    var updateFinished = library.WhenAnyValue(x => x.IsUpdating).FirstAsync(x => !x).PublishLast();
                    updateFinished.Connect();

                    library.UpdateNow();

                    await updateFinished.Timeout(TimeSpan.FromSeconds(5));

                    Assert.Equal(1, library.Songs.Count);
                }
            }

            [Fact]
            public async Task UpdateRemovesArtworkOnlyWithoutReferenceToSong()
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

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithReader(libraryReader).Build())
                {
                    await library.AwaitInitializationAndUpdate();

                    Assert.NotNull(await BlobCache.LocalMachine.GetCreatedAt("artwork-abcdefg"));
                }
            }

            [Fact]
            public async Task UpdateRemovesArtworkWhenSongIsMissing()
            {
                var missingSong = new LocalSong("C://Missing.mp3", TimeSpan.Zero, "artwork-abcdefg");
                await BlobCache.LocalMachine.Insert("artwork-abcdefg", new byte[] { 0, 1 });

                var libraryReader = Substitute.For<ILibraryReader>();
                libraryReader.LibraryExists.Returns(true);
                libraryReader.ReadSongSourcePath().Returns("C://");
                libraryReader.ReadPlaylists().Returns(new List<Playlist>());
                libraryReader.ReadSongs().Returns(new[] { missingSong });

                var fileSystem = new MockFileSystem();
                fileSystem.Directory.CreateDirectory("C://");

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithReader(libraryReader).Build())
                {
                    await library.AwaitInitializationAndUpdate();

                    Assert.Null(await BlobCache.LocalMachine.GetCreatedAt("artwork-abcdefg"));
                }
            }

            [Fact]
            public async Task UpdateRemovesMissingSongsFromLibrary()
            {
                var existingSong = new LocalSong("C://Existing.mp3", TimeSpan.Zero);
                var missingSong = new LocalSong("C://Missing.mp3", TimeSpan.Zero);

                var libraryReader = Substitute.For<ILibraryReader>();
                libraryReader.LibraryExists.Returns(true);
                libraryReader.ReadSongSourcePath().Returns("C://");
                libraryReader.ReadPlaylists().Returns(new List<Playlist>());
                libraryReader.ReadSongs().Returns(new[] { existingSong, missingSong });

                var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { existingSong.OriginalPath, new MockFileData("DontCare") } });

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithReader(libraryReader).Build())
                {
                    await library.AwaitInitializationAndUpdate();

                    Assert.Equal(1, library.Songs.Count());
                }
            }

            [Fact]
            public async Task UpdateRemovesMissingSongsFromPlaylists()
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

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithReader(libraryReader).Build())
                {
                    await library.AwaitInitializationAndUpdate();

                    Assert.Equal(1, library.Playlists[0].Count());
                    Assert.Equal(1, library.Playlists[1].Count());
                }
            }

            [Fact]
            public async Task UpdateRemovesMissingSongWithoutArtworkFromLibraryWhenOtherArtworksArePresent()
            {
                var existingSong = new LocalSong("C://Existing.mp3", TimeSpan.Zero, "artwork-abcdefg");
                var missingSong = new LocalSong("C://Missing.mp3", TimeSpan.Zero);

                var libraryReader = Substitute.For<ILibraryReader>();
                libraryReader.LibraryExists.Returns(true);
                libraryReader.ReadSongSourcePath().Returns("C://");
                libraryReader.ReadPlaylists().Returns(new List<Playlist>());
                libraryReader.ReadSongs().Returns(new[] { existingSong, missingSong });

                var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { existingSong.OriginalPath, new MockFileData("DontCare") } });

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithReader(libraryReader).Build())
                {
                    await library.AwaitInitializationAndUpdate();

                    Assert.Equal(1, library.Songs.Count());
                }
            }
        }

        public class TheUpdateNewMethod
        {
            [Fact]
            public async Task TriggersUpdate()
            {
                var fileSystem = new MockFileSystem();
                fileSystem.Directory.CreateDirectory("C://Test");

                using (var library = new LibraryBuilder().WithFileSystem(fileSystem).Build())
                {
                    library.Initialize();

                    var firstUpdateFinished = library.WhenAnyValue(x => x.IsUpdating).Where(x => !x).ElementAt(1).PublishLast();
                    firstUpdateFinished.Connect();

                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.ChangeSongSourcePath("C://Test", token);

                    await firstUpdateFinished.Timeout(TimeSpan.FromSeconds(5));

                    var updated = library.WhenAnyValue(x => x.IsUpdating).FirstAsync(x => x).PublishLast();
                    updated.Connect();

                    library.UpdateNow();

                    await updated.Timeout(TimeSpan.FromSeconds(5));
                }
            }
        }

        public class TheVoteForPlaylistEntryMethod
        {
            [Fact]
            public void IgnoresAccessPermission()
            {
                var settings = new CoreSettings
                {
                    EnableGuestSystem = true,
                    LockRemoteControl = true,
                    RemoteControlPassword = "Password",
                    MaxVoteCount = 2,
                };

                using (var library = new LibraryBuilder().WithPlaylist().WithSettings(settings).Build())
                {
                    Guid localToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    library.AddSongsToPlaylist(new[] { Helpers.SetupSongMock() }, localToken);
                    library.AddSongsToPlaylist(new[] { Helpers.SetupSongMock() }, localToken);

                    Guid remoteToken = library.RemoteAccessControl.RegisterRemoteAccessToken(Guid.NewGuid());

                    // Guests can vote
                    library.VoteForPlaylistEntry(0, remoteToken);

                    library.RemoteAccessControl.UpgradeRemoteAccess(remoteToken, "Password");

                    // Admins can vote
                    library.VoteForPlaylistEntry(1, remoteToken);
                }
            }

            [Fact]
            public void ThrowsInvalidOperationExceptionIfGuestSystemIsDisabled()
            {
                var settings = new CoreSettings
                {
                    EnableGuestSystem = false
                };

                using (var library = new LibraryBuilder().WithPlaylist().WithSettings(settings).Build())
                {
                    Guid accessToken = library.RemoteAccessControl.RegisterRemoteAccessToken(Guid.NewGuid());

                    library.Initialize();
                    library.AddSongsToPlaylist(new[] { Helpers.SetupSongMock() }, accessToken);

                    Assert.Throws<InvalidOperationException>(() => library.VoteForPlaylistEntry(0, accessToken));
                }
            }
        }
    }
}