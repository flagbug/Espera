using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Tests.Mocks;
using Moq;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public sealed class LibraryTest
    {
        [Test]
        public void AddAndSwitchToPlaylist_SomeGenericName_WorksAsExpected()
        {
            using (var library = Helpers.CreateLibrary())
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
            using (var library = Helpers.CreateLibrary())
            {
                library.AddAndSwitchToPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.AddAndSwitchToPlaylist("Playlist"));
            }
        }

        [Test]
        public void AddLocalSongsAsync_PathIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddLocalSongsAsync(null));
            }
        }

        [Test]
        public void AddPlayist_AddTwoPlaylistsWithSameName_ThrowInvalidOperationException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.AddPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.AddPlaylist("Playlist"));
            }
        }

        [Test]
        public void AddSongsToPlaylist_PartyModeAndMultipleSongsAdded_ThrowsInvalidOperationException()
        {
            var songs = new[] { new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object, new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero).Object };

            using (var library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.AddSongsToPlaylist(songs));
            }
        }

        [Test]
        public void AddSongsToPlaylist_SongListIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.AddSongsToPlaylist(null));
            }
        }

        [Test]
        public void AddSongToPlaylist_SongIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
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

            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                int finished = 0;

                // We need to wait till the second played song has finished and then release our lock,
                // otherwise it would directly call the assertion, without anything changed
                library.SongFinished += (sender, e) =>
                {
                    finished++;

                    if (finished == 2)
                    {
                        eventWait.Set();
                    }
                };

                IEnumerable<Song> songs = new[]
                {
                    jumpSong.Object, cachingSong.Object, cachingSong2.Object, nextSong.Object
                };

                library.AddSongsToPlaylist(songs);

                library.PlaySong(0);

                eventWait.WaitOne();

                var expectedSongs = new[]
                {
                    jumpSong.Object, nextSong.Object, cachingSong.Object, cachingSong2.Object
                };

                Assert.IsTrue(expectedSongs.SequenceEqual(library.CurrentPlaylist.Songs));
            }
        }

        [Test]
        public void CanAddSongToPlaylist_IsAdministrator_ReturnsTrue()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.IsTrue(library.CanAddSongToPlaylist);
            }
        }

        [Test]
        public void CanAddSongToPlaylist_IsPartyModeAndRemainingTimeIsBiggerThanZero_ReturnsTrue()
        {
            using (var library = Helpers.CreateLibraryWithPlaylist())
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
            using (var library = Helpers.CreateLibrary())
            {
                Assert.IsTrue(library.CanChangeTime);
            }
        }

        [Test]
        public void CanChangeTime_IsNotAdministratorAndLockTimeIsFalse_IsTrue()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.LockTime = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsTrue(library.CanChangeTime);
            }
        }

        [Test]
        public void CanChangeTime_IsNotAdministratorAndLockTimeIsTrue_IsFalse()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.LockTime = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsFalse(library.CanChangeTime);
            }
        }

        [Test]
        public void CanChangeVolume_IsAdministrator_IsTrue()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.IsTrue(library.CanChangeVolume);
            }
        }

        [Test]
        public void CanChangeVolume_IsNotAdministratorAndLockVolumeIsFalse_IsTrue()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.LockVolume = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsTrue(library.CanChangeVolume);
            }
        }

        [Test]
        public void CanChangeVolume_IsNotAdministratorAndLockVolumeIsTrue_IsFalse()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.LockVolume = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsFalse(library.CanChangeVolume);
            }
        }

        [Test]
        public void CanSwitchPlaylist_IsAdministrator_IsTrue()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.IsTrue(library.CanSwitchPlaylist);
            }
        }

        [Test]
        public void CanSwitchPlaylist_IsNotAdministratorAndLockPlaylistSwitchingIsFalse_IsTrue()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.LockPlaylistSwitching = false;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsTrue(library.CanSwitchPlaylist);
            }
        }

        [Test]
        public void CanSwitchPlaylist_IsNotAdministratorAndLockPlaylistSwitchingIsTrue_IsFalse()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.LockPlaylistSwitching = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsFalse(library.CanSwitchPlaylist);
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsCorrent_AccessModeIsAdministrator()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToAdmin("TestPassword");

                Assert.AreEqual(AccessMode.Administrator, library.AccessMode);
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsNotCorrent_ThrowsWrongPasswordException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");

                Assert.Throws<WrongPasswordException>(() => library.ChangeToAdmin("WrongPassword"));
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.ChangeToAdmin(null));
            }
        }

        [Test]
        public void ConstructorUpgradesCoreSettingsIfRequired()
        {
            CoreSettings.Default.UpgradeRequired = true;

            using (Helpers.CreateLibrary()) { }

            Assert.IsFalse(CoreSettings.Default.UpgradeRequired);
        }

        [Test]
        public void ContinueSong_IsNotAdmin_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibrary())
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

            using (var library = Helpers.CreateLibraryWithPlaylist())
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
            using (var library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("Password");
                Assert.Throws<InvalidOperationException>(() => library.CreateAdmin("Password"));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsEmpty_ThrowsArgumentException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(String.Empty));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.CreateAdmin(null));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsTestPassword_AdministratorIsCreated()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");

                Assert.IsTrue(library.IsAdministratorCreated);
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsWhitespace_ThrowsArgumentException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(" "));
            }
        }

        [Test]
        public void PauseSong_IsNotAdministratorAndPausingIsLocked_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(library.PauseSong);
            }
        }

        [Test]
        public void PauseSongCallsAudioPlayerPause()
        {
            using (var library = Helpers.CreateLibraryWithPlaylist())
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
        public void PlayNextSong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(library.PlayNextSong);
            }
        }

        [Test]
        public void PlayPreviousSong_PlaylistIsEmpty_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                Assert.Throws<InvalidOperationException>(library.PlayPreviousSong);
            }
        }

        [Test]
        public void PlaysNextSongAutomatically()
        {
            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                var song1 = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
                song1.Setup(p => p.CreateAudioPlayer()).Returns(() => new JumpAudioPlayer());

                var song2 = new Mock<Song>("TestPath2", AudioType.Mp3, TimeSpan.Zero);
                song2.Setup(p => p.CreateAudioPlayer()).Returns(() => new JumpAudioPlayer());

                library.AddSongsToPlaylist(new[] { song1.Object, song2.Object });

                var handle = new ManualResetEvent(false);
                int played = 0;

                library.SongStarted += (sender, e) =>
                {
                    played++;

                    if (played == 2)
                    {
                        handle.Set();
                    }
                };

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
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => library.PlaySong(-1));
            }
        }

        [Test]
        public void PlaySong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.PlaySong(0));
            }
        }

        [Test]
        public void RemoveFromLibrary_IsNotAdministratorAndRemovalIsLocked_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.LockLibraryRemoval = true;

                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.RemoveFromLibrary(Helpers.SetupSongMocks(1)));
            }
        }

        [Test]
        public void RemoveFromLibrary_SongListIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromLibrary(null));
            }
        }

        [Test]
        public void RemoveFromPlaylist_AccessModeIsParty_ThrowsInvalidOperationException()
        {
            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.RemoveFromPlaylist(new[] { 0 }));
            }
        }

        [Test]
        public void RemoveFromPlaylist_IndexesIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<int>)null));
            }
        }

        [Test]
        public void RemoveFromPlaylist_RemoveByIndexes_SongsAreRemovedFromPlaylist()
        {
            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                Song[] songs = Helpers.SetupSongMocks(4);

                library.AddSongsToPlaylist(songs);

                library.RemoveFromPlaylist(new[] { 0, 2 });

                Song[] remaining = library.CurrentPlaylist.Songs.ToArray();

                Assert.AreEqual(songs[1], remaining[0]);
                Assert.AreEqual(songs[3], remaining[1]);
            }
        }

        [Test]
        public void RemoveFromPlaylist_RemoveBySongReference_SongsAreRemovedFromPlaylist()
        {
            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                Song[] songs = Helpers.SetupSongMocks(4, true);

                library.AddSongsToPlaylist(songs);

                library.RemoveFromPlaylist(new[] { songs[0], songs[2] });

                Song[] remaining = library.CurrentPlaylist.Songs.ToArray();

                Assert.AreEqual(songs[1], remaining[0]);
                Assert.AreEqual(songs[3], remaining[1]);
            }
        }

        [Test]
        public void RemoveFromPlaylist_SongIsPlaying_CurrentPlayerIsStopped()
        {
            var audioPlayerMock = new Mock<AudioPlayer>();

            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            songMock.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayerMock.Object);

            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

                library.PlaySong(0);

                library.RemoveFromPlaylist(new[] { 0 });

                audioPlayerMock.Verify(p => p.Stop(), Times.Once());
            }
        }

        [Test]
        public void RemoveFromPlaylist_SongListIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemoveFromPlaylist((IEnumerable<Song>)null));
            }
        }

        [Test]
        public void RemovePlaylist_NoPlaylistExists_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<InvalidOperationException>(() => library.RemovePlaylist("Playlist"));
            }
        }

        [Test]
        public void RemovePlaylist_PlaylistDoesNotExist_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.AddPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.RemovePlaylist("Playlist 2"));
            }
        }

        [Test]
        public void RemovePlaylist_PlaylistNameIsNull_ThrowsArgumentNullException()
        {
            using (var library = Helpers.CreateLibrary())
            {
                Assert.Throws<ArgumentNullException>(() => library.RemovePlaylist(null));
            }
        }

        [Test]
        public void RemovePlaylist_RemoveFirstPlaylist_PlaylistIsRemoved()
        {
            using (var library = Helpers.CreateLibrary())
            {
                library.AddPlaylist("Playlist");

                library.RemovePlaylist("Playlist");

                Assert.IsEmpty(library.Playlists);
            }
        }

        [Test]
        public void SwitchToPlaylist_ChangeToOtherPlaylistAndPlayFirstSong_CurrentSongIndexIsCorrectlySet()
        {
            var blockingPlayer = new Mock<AudioPlayer>();
            blockingPlayer.Setup(p => p.Play()).Callback(() => { });

            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            song.Setup(p => p.CreateAudioPlayer()).Returns(blockingPlayer.Object);

            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                library.AddPlaylist("Playlist 2");
                library.SwitchToPlaylist("Playlist 2");
                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                Assert.AreEqual(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex);
                Assert.AreEqual(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex);
            }
        }

        [Test]
        public void SwitchToPlaylist_ChangeToOtherPlaylistPlaySongAndChangeBack_CurrentSongIndexIsCorrectlySet()
        {
            var blockingPlayer = new Mock<AudioPlayer>();
            blockingPlayer.Setup(p => p.Play()).Callback(() => { });

            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            song.Setup(p => p.CreateAudioPlayer()).Returns(blockingPlayer.Object);

            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                library.AddPlaylist("Playlist 2");
                library.SwitchToPlaylist("Playlist 2");
                library.AddSongToPlaylist(song.Object);

                library.PlaySong(0);

                library.SwitchToPlaylist("Playlist");

                Assert.AreEqual(null, library.Playlists.First(p => p.Name == "Playlist").CurrentSongIndex);
                Assert.AreEqual(0, library.Playlists.First(p => p.Name == "Playlist 2").CurrentSongIndex);
            }
        }

        [Test]
        public void SwitchToPlaylist_PartyModeAndLockPlaylistSwitchingIsTrue_ThrowsInvalidOperationException()
        {
            using (var library = Helpers.CreateLibraryWithPlaylist())
            {
                library.LockPlaylistSwitching = true;

                library.AddPlaylist("Playlist 2");

                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.SwitchToPlaylist("Playlist 2"));
            }
        }

        [Test]
        public void SwitchToPlaylist_PlaySongThenChangePlaylist_NextSongDoesNotPlayWhenSongFinishes()
        {
            using (var library = Helpers.CreateLibraryWithPlaylist())
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