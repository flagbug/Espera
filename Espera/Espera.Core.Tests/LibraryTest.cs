using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests.Mocks;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;

namespace Espera.Core.Tests
{
    [TestFixture]
    public sealed class LibraryTest
    {
        [Test]
        public void AddAndSwitchToPlaylist_SomeGenericName_WorksAsExpected()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.AddAndSwitchToPlaylist("Playlist");

                Assert.AreEqual("Playlist", library.CurrentPlaylist.Name);
                Assert.AreEqual("Playlist", library.Playlists.First().Name);
                Assert.AreEqual(1, library.Playlists.Count());
            }
        }

        [Test]
        public void AddAndSwitchToPlaylist_TwoPlaylistsWithSameName_ThrowsInvalidOperationException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.AddAndSwitchToPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.AddAndSwitchToPlaylist("Playlist"));
            }
        }

        [Test]
        public void AddPlayist_AddTwoPlaylistsWithSameName_ThrowInvalidOperationException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.AddPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.AddPlaylist("Playlist"));
            }
        }

        [Test]
        public void AddPlaylist_NameIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddPlaylist(null));
            }
        }

        [Test]
        public void AddSongsToPlaylist_PartyModeAndMultipleSongsAdded_ThrowsInvalidOperationException()
        {
            var songs = new[] { new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object, new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object };

            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.AddSongsToPlaylist(songs));
            }
        }

        [Test]
        public void AddSongsToPlaylist_SongListIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddSongsToPlaylist(null));
            }
        }

        [Test]
        public void AddSongToPlaylist_SongIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddSongToPlaylist(null));
            }
        }

        [Test]
        public void AutoNextSong_SongIsCaching_SwapSongs()
        {
            var eventWait = new ManualResetEvent(false); // We need this, because Library.PlaySong() pops up a new thread internally and then returns

            var jumpAudioPlayer = new JumpAudioPlayer();

            var jumpSong = new Mock<Song>("JumpSong", AudioType.Mp3, TimeSpan.Zero);
            jumpSong.Setup(p => p.CreateAudioPlayer()).Returns(jumpAudioPlayer);
            jumpSong.SetupGet(p => p.HasToCache).Returns(false);

            var foreverAudioPlayer = new Mock<AudioPlayer>();
            foreverAudioPlayer.SetupProperty(p => p.Volume);
            foreverAudioPlayer.Setup(p => p.Play()).Callback(() => { }); // Never raises SongFinished

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

                library.PlaySong(0);

                if (!eventWait.WaitOne(5000))
                {
                    Assert.Fail();
                }

                var expectedSongs = new[]
                {
                    jumpSong.Object, nextSong.Object, cachingSong.Object, cachingSong2.Object
                };

                Assert.IsTrue(expectedSongs.SequenceEqual(library.CurrentPlaylist.Select(entry => entry.Song)));
            }
        }

        [Test]
        public void CanAddSongToPlaylist_IsAdministrator_ReturnsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.IsTrue(library.CanAddSongToPlaylist);
            }
        }

        [Test]
        public void CanAddSongToPlaylist_IsPartyModeAndRemainingTimeIsBiggerThanZero_ReturnsTrue()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.PlaylistTimeout = TimeSpan.FromMinutes(1);

                library.CreateAdmin("Password");
                library.ChangeToParty();

                Song song = Helpers.SetupSongMock();

                library.AddSongToPlaylist(song);

                Assert.IsFalse(library.CanAddSongToPlaylist);
            }
        }

        [Test]
        public void CanChangeTime_IsAdministrator_IsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.IsTrue(library.CanChangeTime.FirstAsync().Wait());
            }
        }

        [Test]
        public void CanChangeTime_IsNotAdministratorAndLockTimeIsFalse_IsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockTime.Value = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsTrue(library.CanChangeTime.FirstAsync().Wait());
            }
        }

        [Test]
        public void CanChangeTime_IsNotAdministratorAndLockTimeIsTrue_IsFalse()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockTime.Value = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsFalse(library.CanChangeTime.FirstAsync().Wait());
            }
        }

        [Test]
        public void CanChangeVolume_IsAdministrator_IsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.IsTrue(library.CanChangeVolume.FirstAsync().Wait());
            }
        }

        [Test]
        public void CanChangeVolume_IsNotAdministratorAndLockVolumeIsFalse_IsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockVolume.Value = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsTrue(library.CanChangeVolume.FirstAsync().Wait());
            }
        }

        [Test]
        public void CanChangeVolume_IsNotAdministratorAndLockVolumeIsTrue_IsFalse()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockVolume.Value = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsFalse(library.CanChangeVolume.FirstAsync().Wait());
            }
        }

        [Test]
        public void CanSwitchPlaylist_IsAdministrator_IsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.IsTrue(library.CanSwitchPlaylist.FirstAsync().Wait());
            }
        }

        [Test]
        public void CanSwitchPlaylist_IsNotAdministratorAndLockPlaylistSwitchingIsFalse_IsTrue()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockPlaylistSwitching.Value = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsTrue(library.CanSwitchPlaylist.FirstAsync().Wait());
            }
        }

        [Test]
        public void CanSwitchPlaylist_IsNotAdministratorAndLockPlaylistSwitchingIsTrue_IsFalse()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.LockPlaylistSwitching.Value = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsFalse(library.CanSwitchPlaylist.FirstAsync().Wait());
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsCorrent_AccessModeIsAdministrator()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToAdmin("TestPassword");

                library.AccessMode.ObserveOn(ImmediateScheduler.Instance)
                    .Subscribe(x => Assert.AreEqual(AccessMode.Administrator, x));
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsNotCorrent_ThrowsWrongPasswordException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");

                Assert.Throws<WrongPasswordException>(() => library.ChangeToAdmin("WrongPassword"));
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.ChangeToAdmin(null));
            }
        }

        [Test]
        public void ChangeToParty_AdministratorIsNotCreated_ThrowsInvalidOperationException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<InvalidOperationException>(library.ChangeToParty);
            }
        }

        [Test]
        public void ContinueSong_IsNotAdmin_ThrowsInvalidOperationException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(library.ContinueSong);
            }
        }

        [Test]
        public void ContinueSongCallsAudioPlayerPlay()
        {
            var handle = new ManualResetEvent(false);

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Mock<Song> song = Helpers.CreateSongMock();
                var audioPlayer = new Mock<AudioPlayer>();
                audioPlayer.Setup(p => p.Play()).Callback(() => handle.Set());

                song.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                // The library starts a new thread when playing a song, we want to wait till it called the audio player
                // to avoid threading issues and a wrong test result
                handle.WaitOne();

                library.ContinueSong();

                audioPlayer.Verify(p => p.Play(), Times.Exactly(2));
            }
        }

        [Test]
        public void CreateAdmin_AdminAlreadyCreated_ThrowsInvalidOperationException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("Password");
                Assert.Throws<InvalidOperationException>(() => library.CreateAdmin("Password"));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsEmpty_ThrowsArgumentException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(String.Empty));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.CreateAdmin(null));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsTestPassword_AdministratorIsCreated()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");

                Assert.IsTrue(library.IsAdministratorCreated);
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsWhiteSpace_ThrowsArgumentException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(" "));
            }
        }

        [Test]
        public void GetPlaylistByName_PlaylistNameIsNotAPlaylist_ReturnsNull()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.IsNull(library.GetPlaylistByName("Playlist"));
            }
        }

        [Test]
        public void GetPlaylistByName_PlaylistNameIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.GetPlaylistByName(null));
            }
        }

        [Test]
        public void InitializeUpgradesCoreSettingsIfRequired()
        {
            var settings = new Mock<ILibrarySettings>();
            settings.SetupProperty(p => p.UpgradeRequired, true);

            using (Library library = Helpers.CreateLibrary(settings.Object))
            {
                library.Initialize();
            }

            Assert.IsFalse(settings.Object.UpgradeRequired);
        }

        [Test]
        public void PauseSong_IsNotAdministratorAndPausingIsLocked_ThrowsInvalidOperationException()
        {
            var settings = new Mock<ILibrarySettings>();
            settings.SetupProperty(p => p.LockPlayPause, true);

            using (Library library = Helpers.CreateLibrary(settings.Object))
            {
                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(library.PauseSong);
            }
        }

        [Test]
        public void PauseSongCallsAudioPlayerPause()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Mock<Song> song = Helpers.CreateSongMock();
                var audioPlayer = new Mock<AudioPlayer>();

                song.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                library.PauseSong();

                audioPlayer.Verify(p => p.Pause(), Times.Once());
            }
        }

        [Test]
        public void Play_SongIsCorrupted_PlaysNextSong()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var audioPlayer = new Mock<AudioPlayer>();
                audioPlayer.Setup(p => p.Play()).Throws<PlaybackException>();

                Mock<Song> corruptedSong = Helpers.CreateSongMock();
                corruptedSong.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                Mock<Song> nextSong = Helpers.CreateSongMock();
                nextSong.Setup(p => p.CreateAudioPlayer()).Returns(new JumpAudioPlayer());

                library.AddSongsToPlaylist(new[] { corruptedSong.Object, nextSong.Object });

                var handle = new AutoResetEvent(false);

                corruptedSong.Object.IsCorrupted.Subscribe(x => handle.Set());
                library.SongStarted.Subscribe(x => handle.Set());

                library.PlaySong(0);

                handle.WaitOne();
                handle.WaitOne();

                // The test will fail, if the last wait timeouts
            }
        }

        [Test]
        public void Play_ThrowsPlaybackException_SetsSongIsCorruptedToTrue()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var audioPlayer = new Mock<AudioPlayer>();
                audioPlayer.Setup(p => p.Play()).Throws<PlaybackException>();

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                library.AddSongToPlaylist(song.Object);

                var handle = new ManualResetEvent(false);

                song.Object.IsCorrupted.Where(x => x).Subscribe(x => handle.Set());

                library.PlaySong(0);

                handle.WaitOne();

                Assert.IsTrue(song.Object.IsCorrupted.Value);
            }
        }

        [Test]
        public void Play_ThrowsSongLoadException_SetsSongIsCorruptedToTrue()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                var audioPlayer = new Mock<AudioPlayer>();
                audioPlayer.Setup(p => p.Load()).Throws<SongLoadException>();

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayer.Object);

                library.AddSongToPlaylist(song.Object);

                var handle = new ManualResetEvent(false);

                song.Object.IsCorrupted.Where(x => x).Subscribe(x => handle.Set());

                library.PlaySong(0);

                handle.WaitOne();

                Assert.IsTrue(song.Object.IsCorrupted.FirstAsync().Wait());
            }
        }

        [Test]
        public void PlayInstantly_MultipleSongs_PlaysSongsInRow()
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

                library.PlayInstantly(songs.Select(x => x.Object));

                handle.Wait();

                Assert.IsTrue(play1Called);
                Assert.IsTrue(play2Called);
            }
        }

        [Test]
        public void PlayInstantly_OneSong_PlaysSong()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                var player = new Mock<AudioPlayer>();

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(p => p.CreateAudioPlayer()).Returns(player.Object);

                var handle = new ManualResetEventSlim();

                library.SongStarted.Subscribe(x => handle.Set());

                library.PlayInstantly(new[] { song.Object });

                handle.Wait();

                player.Verify(p => p.Play(), Times.Once());
            }
        }

        [Test]
        public void PlayInstantly_SongListIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.PlayInstantly(null));
            }
        }

        [Test]
        public void PlayInstantly_StopsCurrentSong()
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

                library.PlaySong(0);

                library.PlayInstantly(new[] { instantSong.Object });

                if (!handle.Wait(5000))
                {
                    Assert.Fail();
                }
            }
        }

        [Test]
        public void PlayNextSong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(library.PlayNextSong);
            }
        }

        [Test]
        public void PlayPreviousSong_PlaylistIsEmpty_ThrowsInvalidOperationException()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Assert.Throws<InvalidOperationException>(library.PlayPreviousSong);
            }
        }

        [Test]
        public void PlaysNextSongAutomatically()
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

                library.PlaySong(0);

                if (!handle.WaitOne(5000))
                {
                    Assert.Fail("Timout");
                }
            }
        }

        [Test]
        public void PlaySong_IndexIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => library.PlaySong(-1));
            }
        }

        [Test]
        public void PlaySong_UserIsNotAdministratorAndLockPlayPauseIsTrue_ThrowsInvalidOperationException()
        {
            var settings = new Mock<ILibrarySettings>();
            settings.SetupProperty(p => p.LockPlayPause, true);

            using (Library library = Helpers.CreateLibrary(settings.Object))
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.PlaySong(0));
            }
        }

        [Test]
        public void RemoveFromPlaylist_AccessModeIsPartyAndLockPlaylistRemovalIsTrue_ThrowsInvalidOperationException()
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

        [Test]
        public void RemoveFromPlaylist_IndexesIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<int>)null));
            }
        }

        [Test]
        public void RemoveFromPlaylist_RemoveByIndexes_SongsAreRemovedFromPlaylist()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Song[] songs = Helpers.SetupSongMocks(4);

                library.AddSongsToPlaylist(songs);

                library.RemoveFromPlaylist(new[] { 0, 2 });

                Song[] remaining = library.CurrentPlaylist.Select(entry => entry.Song).ToArray();

                Assert.AreEqual(songs[1], remaining[0]);
                Assert.AreEqual(songs[3], remaining[1]);
            }
        }

        [Test]
        public void RemoveFromPlaylist_RemoveBySongReference_SongsAreRemovedFromPlaylist()
        {
            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                Song[] songs = Helpers.SetupSongMocks(4, true);

                library.AddSongsToPlaylist(songs);

                library.RemoveFromPlaylist(new[] { songs[0], songs[2] });

                Song[] remaining = library.CurrentPlaylist.Select(entry => entry.Song).ToArray();

                Assert.AreEqual(songs[1], remaining[0]);
                Assert.AreEqual(songs[3], remaining[1]);
            }
        }

        [Test]
        public void RemoveFromPlaylist_SongIsPlaying_CurrentPlayerIsStopped()
        {
            bool finishedFired = false;

            var audioPlayerMock = new SimpleAudioPlayer();
            audioPlayerMock.PlaybackState.Where(x => x == AudioPlayerState.Stopped).Subscribe(x => finishedFired = true);

            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            songMock.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayerMock);

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

                library.PlaySong(0);

                library.RemoveFromPlaylist(new[] { 0 });
            }

            Assert.IsTrue(finishedFired);
        }

        [Test]
        public void RemoveFromPlaylist_SongListIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<Song>)null));
            }
        }

        [Test]
        public void RemovePlaylist_PlaylistNameIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemovePlaylist(null));
            }
        }

        [Test]
        public void RemovePlaylist_RemoveFirstPlaylist_PlaylistIsRemoved()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                library.AddPlaylist("Playlist");

                library.RemovePlaylist(library.GetPlaylistByName("Playlist"));

                Assert.IsEmpty(library.Playlists);
            }
        }

        [Test]
        public void Save_LibraryWithTemporaryPlaylist_DoesntSaveTemporaryPlaylist()
        {
            var libraryWriter = new Mock<ILibraryWriter>();
            libraryWriter.Setup(x => x.Write(It.IsAny<IEnumerable<LocalSong>>(), It.IsAny<IEnumerable<Playlist>>(), It.IsAny<string>()))
                .Callback<IEnumerable<LocalSong>, IEnumerable<Playlist>>((songs, playlists) => Assert.AreEqual(1, playlists.Count()));

            using (Library library = Helpers.CreateLibrary(libraryWriter.Object))
            {
                library.AddAndSwitchToPlaylist("Playlist");

                Mock<Song> song = Helpers.CreateSongMock();
                song.Setup(x => x.CreateAudioPlayer()).Returns(new JumpAudioPlayer());

                library.PlayInstantly(new[] { song.Object });

                library.Save();
            }

            libraryWriter.Verify(x => x.Write(It.IsAny<IEnumerable<LocalSong>>(), It.IsAny<IEnumerable<Playlist>>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void SwitchToPlaylist_ChangeToOtherPlaylistAndPlayFirstSong_CurrentSongIndexIsCorrectlySet()
        {
            var blockingPlayer = new Mock<AudioPlayer>();
            blockingPlayer.Setup(p => p.Play()).Callback(() => { });

            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            song.Setup(p => p.CreateAudioPlayer()).Returns(blockingPlayer.Object);

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                library.AddPlaylist("Playlist 2");
                library.SwitchToPlaylist(library.Playlists.Last());
                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                Assert.AreEqual(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex.Value);
                Assert.AreEqual(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex.Value);
            }
        }

        [Test]
        public void SwitchToPlaylist_ChangeToOtherPlaylistPlaySongAndChangeBack_CurrentSongIndexIsCorrectlySet()
        {
            var blockingPlayer = new Mock<AudioPlayer>();
            blockingPlayer.Setup(p => p.Play()).Callback(() => { });

            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            song.Setup(p => p.CreateAudioPlayer()).Returns(blockingPlayer.Object);

            using (Library library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                library.AddPlaylist("Playlist 2");
                library.SwitchToPlaylist(library.GetPlaylistByName("Playlist 2"));
                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                library.SwitchToPlaylist(library.GetPlaylistByName("Playlist"));

                Assert.AreEqual(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex.Value);
                Assert.AreEqual(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex.Value);
            }
        }

        [Test]
        public void SwitchToPlaylist_PartyModeAndLockPlaylistSwitchingIsTrue_ThrowsInvalidOperationException()
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

        [Test]
        public void SwitchToPlaylist_PlaylistIsNull_ThrowsArgumentNullException()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.SwitchToPlaylist(null));
            }
        }

        [Test]
        public void SwitchToPlaylist_PlaySongThenChangePlaylist_NextSongDoesNotPlayWhenSongFinishes()
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
                library.PlaySong(0);

                library.AddAndSwitchToPlaylist("Playlist2");

                handle.Set();

                Assert.IsFalse(played);
            }
        }
    }
}