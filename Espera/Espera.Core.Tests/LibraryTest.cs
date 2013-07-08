using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests.Mocks;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
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
                new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object,
                new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object
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
        public async void CanChangeTimeIsFalseIfIsNotAdministratorAndLockTimeIsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockTime.Value = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.False(await library.CanChangeTime.FirstAsync());
            }
        }

        [Fact]
        public async void CanChangeTimeIsTrueIfIsAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.True(await library.CanChangeTime.FirstAsync());
            }
        }

        [Fact]
        public async void CanChangeTimeIsTrueIfIsNotAdministratorAndLockTimeIsFalse()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockTime.Value = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.True(await library.CanChangeTime.FirstAsync());
            }
        }

        [Fact]
        public async void CanChangeVolumeIsFalseIsNotAdministratorAndLockVolumeIsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockVolume.Value = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.False(await library.CanChangeVolume.FirstAsync());
            }
        }

        [Fact]
        public async void CanChangeVolumeIsTrueIfIsAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.True(await library.CanChangeVolume.FirstAsync());
            }
        }

        [Fact]
        public async void CanChangeVolumeIsTrueIfIsNotAdministratorAndLockVolumeIsFalse()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockVolume.Value = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.True(await library.CanChangeVolume.FirstAsync());
            }
        }

        [Fact]
        public async void CanSwitchPlaylistIsFalseIfIsNotAdministratorAndLockPlaylistSwitchingIsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockPlaylistSwitching.Value = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.False(await library.CanSwitchPlaylist.FirstAsync());
            }
        }

        [Fact]
        public async void CanSwitchPlaylistIsTrueIfIsNotAdministratorAndLockPlaylistSwitchingIsFalse()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockPlaylistSwitching.Value = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.True(await library.CanSwitchPlaylist.FirstAsync());
            }
        }

        [Fact]
        public async void CanSwitchPlaylistIsTrueIsAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.True(await library.CanSwitchPlaylist.FirstAsync());
            }
        }

        [Fact]
        public async void ChangeToAdminChangesAccessModeToAdministratorIfPasswordIsCorrect()
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
        public async void ContinueSongCallsAudioPlayerPlay()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Mock<Song> song = Helpers.CreateSongMock();
                var audioPlayer = new Mock<AudioPlayer>();

                song.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                library.AddSongToPlaylist(song.Object);

                await library.PlaySongAsync(0);

                await library.ContinueSongAsync();

                audioPlayer.Verify(p => p.PlayAsync(), Times.Exactly(2));
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
        public void InitializeUpgradesCoreSettingsIfRequired()
        {
            var settings = new Mock<ILibrarySettings>();
            settings.SetupProperty(p => p.UpgradeRequired, true);

            using (Library library = Helpers.CreateLibrary(settings.Object))
            {
                library.Initialize();
            }

            Assert.False(settings.Object.UpgradeRequired);
        }

        [Fact]
        public async void PauseSongCallsAudioPlayerPause()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Mock<Song> song = Helpers.CreateSongMock();
                var audioPlayer = new Mock<AudioPlayer>();

                song.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                library.AddSongToPlaylist(song.Object);

                await library.PlaySongAsync(0);

                await library.PauseSongAsync();

                audioPlayer.Verify(p => p.PauseAsync(), Times.Once());
            }
        }

        [Fact]
        public void PauseSongThrowsInvalidOperationExceptionIfIsNotAdministratorAndPausingIsLocked()
        {
            var settings = new Mock<ILibrarySettings>();
            settings.SetupProperty(p => p.LockPlayPause, true);

            using (Library library = Helpers.CreateLibrary(settings.Object))
            {
                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(async () => await library.PauseSongAsync());
            }
        }

        [Fact]
        public async void PlayInstantlyPlaysMultipleSongsInARow()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                bool play1Called = false;
                bool play2Called = false;

                var player1 = new JumpAudioPlayer();
                player1.PlaybackState.Where(x => x == AudioPlayerState.Playing).Subscribe(x => play1Called = true);

                var player2 = new JumpAudioPlayer();
                player2.PlaybackState.Where(x => x == AudioPlayerState.Playing).Subscribe(x => play2Called = true);

                Mock<Song>[] songs = Helpers.CreateSongMocks(2, false);
                songs[0].Setup(p => p.CreateAudioPlayer()).Returns(player1);
                songs[1].Setup(p => p.CreateAudioPlayer()).Returns(player2);

                var handle = new CountdownEvent(2);

                library.SongStarted.Subscribe(x => handle.Signal());

                await library.PlayInstantlyAsync(songs.Select(x => x.Object));

                handle.Wait();
                handle.Wait();

                Assert.True(play1Called);
                Assert.True(play2Called);
            }
        }

        [Fact]
        public async void PlayInstantlySmokeTest()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                var player = new Mock<AudioPlayer>();

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(p => p.CreateAudioPlayer()).Returns(player.Object);

                await library.PlayInstantlyAsync(new[] { song.Object });

                player.Verify(p => p.PlayAsync(), Times.Once());
            }
        }

        [Fact]
        public async void PlayInstantlyStopsCurrentSong()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.SwitchToPlaylist(library.Playlists.First());

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(x => x.CreateAudioPlayer()).Returns(new JumpAudioPlayer());

                Mock<Song> instantSong = Helpers.CreateSongMock();
                instantSong.Setup(x => x.CreateAudioPlayer()).Returns(new JumpAudioPlayer());

                library.AddSongToPlaylist(song.Object);

                var handle = new ManualResetEventSlim();

                library.PlaybackState
                    .Where(x => x == AudioPlayerState.Finished)
                    .Subscribe(x => handle.Set());

                await library.PlaySongAsync(0);

                await library.PlayInstantlyAsync(new[] { instantSong.Object });

                if (!handle.Wait(5000))
                {
                    Assert.True(false, "Timeout");
                }
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
        public async void PlayJumpsOverCorruptedSong()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var audioPlayer = new Mock<AudioPlayer>();
                audioPlayer.Setup(p => p.PlayAsync()).Throws<PlaybackException>();

                Mock<Song> corruptedSong = Helpers.CreateSongMock();
                corruptedSong.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                Mock<Song> nextSong = Helpers.CreateSongMock();
                nextSong.Setup(p => p.CreateAudioPlayer()).Returns(new JumpAudioPlayer());

                library.AddSongsToPlaylist(new[] { corruptedSong.Object, nextSong.Object });

                var handle = new AutoResetEvent(false);

                corruptedSong.Object.IsCorrupted.Subscribe(x => handle.Set());
                library.SongStarted.Subscribe(x => handle.Set());

                await library.PlaySongAsync(0);

                handle.WaitOne(5000);
                handle.WaitOne(5000);

                // The test will fail, if the last wait timeouts
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
        public async void PlaySetsSongIsCorruptedToTrueÍfFailing()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var audioPlayer = new Mock<AudioPlayer>();
                audioPlayer.Setup(p => p.PlayAsync()).Throws<PlaybackException>();

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                library.AddSongToPlaylist(song.Object);

                var handle = new ManualResetEvent(false);

                song.Object.IsCorrupted.Where(x => x).Subscribe(x => handle.Set());

                await library.PlaySongAsync(0);

                handle.WaitOne();

                Assert.True(song.Object.IsCorrupted.Value);
            }

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var audioPlayer = new Mock<AudioPlayer>();
                audioPlayer.Setup(p => p.LoadAsync()).Throws<SongLoadException>();

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                library.AddSongToPlaylist(song.Object);

                var handle = new ManualResetEvent(false);

                song.Object.IsCorrupted.Where(x => x).Subscribe(x => handle.Set());

                await library.PlaySongAsync(0);

                handle.WaitOne();

                Assert.True(song.Object.IsCorrupted.Value);
            }
        }

        [Fact]
        public async void PlaysNextSongAutomatically()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
                song1.Setup(p => p.CreateAudioPlayer()).Returns(() => new JumpAudioPlayer());

                var song2 = new Mock<Song>("TestPath2", AudioType.Mp3, TimeSpan.Zero);
                song2.Setup(p => p.CreateAudioPlayer()).Returns(() => new JumpAudioPlayer());

                library.AddSongsToPlaylist(new[] { song1.Object, song2.Object });

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
            var settings = new Mock<ILibrarySettings>();
            settings.SetupProperty(p => p.LockPlayPause, true);

            using (Library library = Helpers.CreateLibrary(settings.Object))
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
            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            var settings = new Mock<ILibrarySettings>();
            settings.SetupProperty(p => p.LockPlaylistRemoval, true);

            using (Library library = Helpers.CreateLibraryWithPlaylist(settings: settings.Object))
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

                library.CreateAdmin("SomePassword");

                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.RemoveFromPlaylist(new[] { 0 }));
            }
        }

        [Fact]
        public async void RemoveFromPlaylistWhileSongIsPlayingStopsCurrentSong()
        {
            bool finishedFired = false;

            var audioPlayerMock = new SimpleAudioPlayer();
            audioPlayerMock.PlaybackState.Where(x => x == AudioPlayerState.Stopped).Subscribe(x => finishedFired = true);

            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            songMock.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayerMock);

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

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
        public async void SaveDoesNotSaveTemporaryPlaylist()
        {
            var libraryWriter = new Mock<ILibraryWriter>();
            libraryWriter.Setup(x => x.Write(It.IsAny<IEnumerable<LocalSong>>(), It.IsAny<IEnumerable<Playlist>>(), It.IsAny<string>()))
                .Callback<IEnumerable<LocalSong>, IEnumerable<Playlist>, string>((songs, playlists, songSourcePath) =>
                    Assert.Equal(1, playlists.Count()));

            using (Library library = Helpers.CreateLibrary(libraryWriter.Object))
            {
                library.AddAndSwitchToPlaylist("Playlist");

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(x => x.CreateAudioPlayer()).Returns(new JumpAudioPlayer());

                await library.PlayInstantlyAsync(new[] { song.Object });

                library.Save();
            }

            libraryWriter.Verify(x => x.Write(It.IsAny<IEnumerable<LocalSong>>(), It.IsAny<IEnumerable<Playlist>>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public async void SongsAreSwappedIfTheNextSongIsCaching()
        {
            var eventWait = new ManualResetEvent(false); // We need this, because Library.PlaySong() pops up a new thread internally and then returns

            var jumpAudioPlayer = new JumpAudioPlayer();

            var jumpSong = new Mock<Song>("JumpSong", AudioType.Mp3, TimeSpan.Zero);
            jumpSong.Setup(p => p.CreateAudioPlayer()).Returns(jumpAudioPlayer);
            jumpSong.SetupGet(p => p.HasToCache).Returns(false);

            var foreverAudioPlayer = new Mock<AudioPlayer>();
            foreverAudioPlayer.SetupProperty(p => p.Volume);
            foreverAudioPlayer.Setup(p => p.PlayAsync()).Callback(() => { }); // Never raises SongFinished

            var cachingSong = new Mock<Song>("CachingSong", AudioType.Mp3, TimeSpan.Zero);
            cachingSong.SetupGet(p => p.HasToCache).Returns(true);
            cachingSong.Setup(p => p.CreateAudioPlayer()).Returns(foreverAudioPlayer.Object);

            var cachingSong2 = new Mock<Song>("CachingSong2", AudioType.Mp3, TimeSpan.Zero);
            cachingSong2.SetupGet(p => p.HasToCache).Returns(true);

            var nextSong = new Mock<Song>("NextSong", AudioType.Mp3, TimeSpan.Zero);
            nextSong.Setup(p => p.CreateAudioPlayer()).Returns(jumpAudioPlayer);
            nextSong.SetupGet(p => p.HasToCache).Returns(false);

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                int finished = 0;

                // We need to wait till the second played song has finished and then release our lock,
                // otherwise it would directly call the assertion, without anything changed
                library.PlaybackState.Where(x => x == AudioPlayerState.Finished).Subscribe(x =>
                {
                    finished++;

                    if (finished == 2)
                    {
                        eventWait.Set();
                    }
                });

                IEnumerable<Song> songs = new[]
                {
                    jumpSong.Object, cachingSong.Object, cachingSong2.Object, nextSong.Object
                };

                library.AddSongsToPlaylist(songs);

                await library.PlaySongAsync(0);

                if (!eventWait.WaitOne(5000))
                {
                    Assert.True(false, "Timeout");
                }

                var expectedSongs = new[]
                {
                    jumpSong.Object, nextSong.Object, cachingSong.Object, cachingSong2.Object
                };

                Assert.Equal(expectedSongs, library.CurrentPlaylist.Select(entry => entry.Song));
            }
        }

        [Fact]
        public async void SwitchingPlaylistAndPlayingSongsChangesCurrentSongIndex()
        {
            var blockingPlayer = new Mock<AudioPlayer>();
            blockingPlayer.Setup(p => p.PlayAsync()).Callback(() => { });

            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            song.Setup(p => p.CreateAudioPlayer()).Returns(blockingPlayer.Object);

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongToPlaylist(song.Object);

                await library.PlaySongAsync(0);

                library.AddPlaylist("Playlist 2");
                library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2"));
                library.AddSongToPlaylist(song.Object);

                await library.PlaySongAsync(0);

                library.SwitchToPlaylist(library.GetPlaylistByName("Playlist"));

                Assert.Equal(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex.Value);
                Assert.Equal(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex.Value);
            }
        }

        [Fact]
        public async void SwitchingPlaylistPreventsNextSongFromPlaying()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var handle = new ManualResetEvent(false);

                var player = new HandledAudioPlayer(handle);

                var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
                song.Setup(p => p.CreateAudioPlayer()).Returns(player);

                bool played = false;

                var notPlayedSong = new Mock<Song>("TestPath2", AudioType.Mp3, TimeSpan.Zero);
                notPlayedSong.Setup(p => p.CreateAudioPlayer())
                    .Returns(new Mock<AudioPlayer>().Object)
                    .Callback(() => played = true);

                library.AddSongToPlaylist(song.Object);
                await library.PlaySongAsync(0);

                library.AddAndSwitchToPlaylist("Playlist2");

                handle.Set();

                Assert.False(played);
            }
        }

        [Fact]
        public async void SwitchToPlaylistSetsCurrentSongIndexIfChangingfToOtherPlaylistAndPlayingFirstSong()
        {
            var blockingPlayer = new Mock<AudioPlayer>();
            blockingPlayer.Setup(p => p.PlayAsync()).Callback(() => { });

            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            song.Setup(p => p.CreateAudioPlayer()).Returns(blockingPlayer.Object);

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongToPlaylist(song.Object);

                await library.PlaySongAsync(0);

                library.AddPlaylist("Playlist 2");
                library.SwitchToPlaylist(library.Playlists.Last());
                library.AddSongToPlaylist(song.Object);

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
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.LockPlaylistSwitching.Value = true;

                library.AddPlaylist("Playlist 2");

                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2")));
            }
        }
    }
}