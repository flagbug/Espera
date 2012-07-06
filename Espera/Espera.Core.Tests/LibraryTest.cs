using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Espera.Core.Audio;
using Espera.Core.Library;
using Espera.Core.Tests.Mocks;
using Moq;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class LibraryTest
    {
        [Test]
        public void AddSongsToPlaylist_PartyModeAndMultipleSongsAdded_ThrowsInvalidOperationException()
        {
            var songs = new[] { new LocalSong("TestPath", AudioType.Mp3, TimeSpan.Zero), new LocalSong("TestPath", AudioType.Mp3, TimeSpan.Zero) };

            using (var library = new Library.Library())
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

            using (var library = new Library.Library())
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
        public void ChangeToAdmin_PasswordIsCorrent_AccessModeIsAdministrator()
        {
            using (var library = new Library.Library())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToAdmin("TestPassword");

                Assert.AreEqual(AccessMode.Administrator, library.AccessMode);
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsNotCorrent_ThrowsInvalidOperationException()
        {
            using (var library = new Library.Library())
            {
                library.CreateAdmin("TestPassword");

                Assert.Throws<InvalidPasswordException>(() => library.ChangeToAdmin("WrongPassword"));
            }
        }

        [Test]
        public void ChangeToAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            using (var library = new Library.Library())
            {
                Assert.Throws<ArgumentNullException>(() => library.ChangeToAdmin(null));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsEmpty_ThrowsArgumentException()
        {
            using (var library = new Library.Library())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(String.Empty));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            using (var library = new Library.Library())
            {
                Assert.Throws<ArgumentNullException>(() => library.CreateAdmin(null));
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsTestPassword_AdministratorIsCreated()
        {
            using (var library = new Library.Library())
            {
                library.CreateAdmin("TestPassword");

                Assert.IsTrue(library.IsAdministratorCreated);
            }
        }

        [Test]
        public void CreateAdmin_PasswordIsWhitespace_ThrowsArgumentException()
        {
            using (var library = new Library.Library())
            {
                Assert.Throws<ArgumentException>(() => library.CreateAdmin(" "));
            }
        }

        [Test]
        public void PlayNextSong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            using (var library = new Library.Library())
            {
                library.CreateAdmin("TestPassword");
                library.ChangeToParty();

                Assert.Throws<InvalidOperationException>(() => library.PlayNextSong());
            }
        }

        [Test]
        public void PlayPreviousSong_PlaylistIsEmpty_ThrowsInvalidOperationException()
        {
            using (var library = new Library.Library())
            {
                Assert.Throws<InvalidOperationException>(() => library.PlayPreviousSong());
            }
        }

        [Test]
        public void PlaySong_IndexIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            using (var library = new Library.Library())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => library.PlaySong(-1));
            }
        }

        [Test]
        public void PlaySong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            using (var library = new Library.Library())
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

            using (var library = new Library.Library())
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

            using (var library = new Library.Library())
            {
                library.AddSongsToPlaylist(new[] { songMock.Object });

                library.PlaySong(0);

                library.RemoveFromPlaylist(new[] { 0 });

                audioPlayerMock.Verify(p => p.Stop(), Times.Once());
            }
        }

        [Test]
        public void AddPlaylist_AddSecondPlaylist_NameIsCorrect()
        {
            using (var library = new Library.Library())
            {
                library.AddPlaylist();

                Assert.AreEqual("New Playlist 2", library.Playlists.ToList()[1].Name);
            }
        }

        [Test]
        public void AddPlaylist_AddThirdPlaylist_NameIsCorrect()
        {
            using (var library = new Library.Library())
            {
                library.AddPlaylist();
                library.AddPlaylist();

                Assert.AreEqual("New Playlist 3", library.Playlists.ToList()[2].Name);
            }
        }
    }
}