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
    public class LibraryTest
    {
        [Test]
        public void AddAndSwitchToPlaylist_SomeGenericName_WorksAsExpected()
        {
            using (var library = new Library())
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
            using (var library = new Library())
            {
                library.AddAndSwitchToPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.AddAndSwitchToPlaylist("Playlist"));
            }
        }

        [Test]
        public void AddPlayist_AddTwoPlaylistsWithSameName_ThrowInvalidOperationException()
        {
            using (var library = new Library())
            {
                library.AddPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.AddPlaylist("Playlist"));
            }
        }

        [Test]
        public void AddSongsToPlaylist_PartyModeAndMultipleSongsAdded_ThrowsInvalidOperationException()
        {
            var songs = new[] { new LocalSong("TestPath", AudioType.Mp3, TimeSpan.Zero), new LocalSong("TestPath", AudioType.Mp3, TimeSpan.Zero) };

            using (var library = new Library())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.AddSongsToPlaylist(songs));
            }
        }

        [Test]
        public void AutoNextSong_SongIsCaching_SwapSongs()
        {
            var eventWait = new ManualResetEvent(false); // We need this, because Library.PlaySong() pops up a new thread interally and then returns

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

            using (var library = CreateLibraryWithPlaylist("Playlist"))
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

                IEnumerable<Song> expectedSongs = new[]
                {
                    jumpSong.Object, nextSong.Object, cachingSong.Object, cachingSong2.Object
                };

                Assert.IsTrue(expectedSongs.SequenceEqual(library.CurrentPlaylist.Songs));
            }
        }

        [Test]
        public void CanChangeVolume_IsAdministrator_IsTrue()
        {
            using (var library = new Library())
            {
                Assert.IsTrue(library.CanChangeVolume);
            }
        }

        [Test]
        public void CanChangeVolume_IsNotAdministratorAndLockVolumeIsFalse_IsTrue()
        {
            using (var library = new Library())
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
            using (var library = new Library())
            {
                library.LockVolume = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsFalse(library.CanChangeVolume);
            }
        }

        [Test]
        public void CanChangeTime_IsAdministrator_IsTrue()
        {
            using (var library = new Library())
            {
                Assert.IsTrue(library.CanChangeTime);
            }
        }

        [Test]
        public void CanChangeTime_IsNotAdministratorAndLockTimeIsFalse_IsTrue()
        {
            using (var library = new Library())
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
            using (var library = new Library())
            {
                library.LockTime = true;

                library.CreateAdmin("password");
                library.ChangeToParty();

                Assert.IsFalse(library.CanChangeTime);
            }
        }

        [Test]
        public void CanSwitchPlaylist_IsAdministrator_IsTrue()
        {
            using(var library = new Library())
            {
                Assert.IsTrue(library.CanSwitchPlaylist);
            }
        }

        [Test]
        public void CanSwitchPlaylist_IsNotAdministratorAndLockPlaylistSwitchingIsFalse_IsTrue()
        {
            using(var library = new Library())
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
            using(var library = new Library())
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
            using (var library = new Library())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToAdmin("TestPassword");

                Assert.AreEqual(AccessMode.Administrator, library.AccessMode);
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsNotCorrent_ThrowsInvalidOperationException()
        {
            using (var library = new Library())
            {
                library.CreateAdmin("TestPassword");

                Assert.Throws<InvalidPasswordException>(() => library.ChangeToAdmin("WrongPassword"));
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            using (var library = new Library())
            {
                Assert.Throws<ArgumentNullException>(() => library.ChangeToAdmin(null));
            }
        }

        [Test]
        public void SwitchToPlaylist_ChangeToOtherPlaylistAndPlayFirstSong_CurrentSongIndexIsCorrectlySet()
        {
            var blockingPlayer = new Mock<AudioPlayer>();
            blockingPlayer.Setup(p => p.Play()).Callback(() => { });

            var song = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            song.Setup(p => p.CreateAudioPlayer()).Returns(blockingPlayer.Object);

            using (var library = CreateLibraryWithPlaylist("Playlist"))
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

            using (var library = CreateLibraryWithPlaylist("Playlist"))
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
        public void SwitchToPlaylist_PlaySongThenChangePlaylist_NextSongDoesNotPlayWhenSongFinishes()
        {
            using (var library = CreateLibraryWithPlaylist("Playlist"))
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

        [Test]
        public void SwitchToPlaylist_PartyModeAndLockPlaylistSwitchingIsTrue_ThrowsInvalidOperationException()
        {
            using(var library = CreateLibraryWithPlaylist("Playlist"))
            {
                library.LockPlaylistSwitching = true;

                library.AddPlaylist("Playlist 2");

                library.CreateAdmin("Password");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.SwitchToPlaylist("Playlist 2"));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsEmpty_ThrowsArgumentException()
        {
            using (var library = new Library())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(String.Empty));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            using (var library = new Library())
            {
                Assert.Throws<ArgumentNullException>(() => library.CreateAdmin(null));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsTestPassword_AdministratorIsCreated()
        {
            using (var library = new Library())
            {
                library.CreateAdmin("TestPassword");

                Assert.IsTrue(library.IsAdministratorCreated);
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsWhitespace_ThrowsArgumentException()
        {
            using (var library = new Library())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(" "));
            }
        }

        [Test]
        public void PlayNextSong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            using (var library = new Library())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(library.PlayNextSong);
            }
        }

        [Test]
        public void PlayPreviousSong_PlaylistIsEmpty_ThrowsInvalidOperationException()
        {
            using (var library = CreateLibraryWithPlaylist("Playlist"))
            {
                Assert.Throws<InvalidOperationException>(library.PlayPreviousSong);
            }
        }

        [Test]
        public void PlaysNextSongAutomatically()
        {
            using (var library = CreateLibraryWithPlaylist("Playlist"))
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
            using (var library = new Library())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => library.PlaySong(-1));
            }
        }

        [Test]
        public void PlaySong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            using (var library = new Library())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.PlaySong(0));
            }
        }

        [Test]
        public void RemoveFromPlaylist_AccessModeIsParty_ThrowsInvalidOperationException()
        {
            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            using (var library = CreateLibraryWithPlaylist("Playlist"))
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.RemoveFromPlaylist(new[] { 0 }));
            }
        }

        [Test]
        public void RemoveFromPlaylist_SongIsPlaying_CurrentPlayerIsStopped()
        {
            var audioPlayerMock = new Mock<AudioPlayer>();

            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            songMock.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayerMock.Object);

            using (var library = CreateLibraryWithPlaylist("Playlist"))
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

                library.PlaySong(0);

                library.RemoveFromPlaylist(new[] { 0 });

                audioPlayerMock.Verify(p => p.Stop(), Times.Once());
            }
        }

        [Test]
        public void RemovePlaylist_NoPlaylistExists_ThrowsInvalidOperationException()
        {
            using (var library = new Library())
            {
                Assert.Throws<InvalidOperationException>(() => library.RemovePlaylist("Playlist"));
            }
        }

        [Test]
        public void RemovePlaylist_PlaylistDoesNotExist_ThrowsInvalidOperationException()
        {
            using (var library = new Library())
            {
                library.AddPlaylist("Playlist");

                Assert.Throws<InvalidOperationException>(() => library.RemovePlaylist("Playlist 2"));
            }
        }

        [Test]
        public void RemovePlaylist_RemoveFirstPlaylist_PlaylistIsRemoved()
        {
            using (var library = new Library())
            {
                library.AddPlaylist("Playlist");

                library.RemovePlaylist("Playlist");

                Assert.IsEmpty(library.Playlists);
            }
        }

        private static Library CreateLibraryWithPlaylist(string playlistName)
        {
            var library = new Library();
            library.AddAndSwitchToPlaylist(playlistName);

            return library;
        }
    }
}