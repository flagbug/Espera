using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Espera.Core.Audio;
using Espera.Core.Library;
using Espera.Core.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Espera.Core.Tests
{
    [TestClass]
    public class LibraryTest
    {
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AddSongsToPlaylist_PartyModeAndMultipleSongsAdded_ThrowsInvalidOperationException()
        {
            var songs = new[] { new LocalSong("TestPath", AudioType.Mp3, TimeSpan.Zero), new LocalSong("TestPath", AudioType.Mp3, TimeSpan.Zero) };

            var library = new Library.Library();

            library.CreateAdmin("TestPassword");
            library.ChangeToParty();

            library.AddSongsToPlaylist(songs);

            library.Dispose();
        }

        [TestMethod]
        public void AutoNextSong_SongIsCaching_SwapSongs()
        {
            var eventWait = new ManualResetEvent(false); // We need this, because Library.PlaySong() pops up a new thread interally and then returns

            var jumpAudioPlayer = new JumpAudioPlayer();
            jumpAudioPlayer.SongFinished += (sender, e) => eventWait.Set();

            var jumpSong = new Mock<Song>("JumpSong", AudioType.Mp3, TimeSpan.Zero);
            jumpSong.Setup(p => p.CreateAudioPlayer()).Returns(jumpAudioPlayer);
            jumpSong.SetupGet(p => p.HasToCache).Returns(false);

            var cachingSong = new Mock<Song>("CachingSong", AudioType.Mp3, TimeSpan.Zero);
            cachingSong.SetupGet(p => p.HasToCache).Returns(true);

            var foreverAudioPlayer = new Mock<AudioPlayer>();

            var nextSong = new Mock<Song>("NextSong", AudioType.Mp3, TimeSpan.Zero);
            nextSong.Setup(p => p.CreateAudioPlayer()).Returns(foreverAudioPlayer.Object);
            nextSong.SetupGet(p => p.HasToCache).Returns(false);

            var library = new Library.Library();

            IEnumerable<Song> songs = new[] { jumpSong.Object, cachingSong.Object, nextSong.Object };
            library.AddSongsToPlaylist(songs);

            library.PlaySong(0);

            eventWait.WaitOne();

            IEnumerable<Song> expectedSongs = new[] { jumpSong.Object, nextSong.Object, cachingSong.Object };

            Assert.IsTrue(expectedSongs.SequenceEqual(library.Playlist));

            library.Dispose();
        }

        [TestMethod]
        public void ChangeToAdmin_PasswordIsCorrent_AccessModeIsAdministrator()
        {
            var library = new Library.Library();
            library.CreateAdmin("TestPassword");
            library.ChangeToAdmin("TestPassword");

            Assert.AreEqual(AccessMode.Administrator, library.AccessMode);

            library.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidPasswordException))]
        public void ChangeToAdmin_PasswordIsNotCorrent_ThrowsInvalidOperationException()
        {
            var library = new Library.Library();
            library.CreateAdmin("TestPassword");

            try
            {
                library.ChangeToAdmin("WrongPassword");
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ChangeToAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            var library = new Library.Library();

            try
            {
                library.ChangeToAdmin(null);
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CreateAdmin_PasswordIsEmpty_ThrowsArgumentException()
        {
            var library = new Library.Library();

            try
            {
                library.CreateAdmin(String.Empty);
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CreateAdmin_PasswordIsNull_ThrowsArgumentNullException()
        {
            var library = new Library.Library();

            try
            {
                library.CreateAdmin(null);
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        public void CreateAdmin_PasswordIsTestPassword_AdministratorIsCreated()
        {
            var library = new Library.Library();

            library.CreateAdmin("TestPassword");

            Assert.IsTrue(library.IsAdministratorCreated);

            library.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CreateAdmin_PasswordIsWhitespace_ThrowsArgumentException()
        {
            var library = new Library.Library();

            try
            {
                library.CreateAdmin(" ");
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void PlayNextSong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            var library = new Library.Library();
            library.CreateAdmin("TestPassword");
            library.ChangeToParty();

            try
            {
                library.PlayNextSong();
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void PlayPreviousSong_PlaylistIsEmpty_ThrowsInvalidOperationException()
        {
            var library = new Library.Library();

            try
            {
                library.PlayPreviousSong();
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void PlaySong_IndexIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var library = new Library.Library();

            try
            {
                library.PlaySong(-1);
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void PlaySong_UserIsNotAdministrator_ThrowsInvalidOperationException()
        {
            var library = new Library.Library();
            library.CreateAdmin("TestPassword");
            library.ChangeToParty();

            try
            {
                library.PlaySong(0);
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveFromPlaylist_AccessModeIsParty_ThrowsInvalidOperationException()
        {
            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);

            var library = new Library.Library();
            library.ChangeToParty();

            library.AddSongsToPlaylist(new[] { songMock.Object });

            try
            {
                library.RemoveFromPlaylist(new[] { 0 });
            }

            finally
            {
                library.Dispose();
            }
        }

        [TestMethod]
        public void RemoveFromPlaylist_SongIsPlaying_CurrentPlayerIsStopped()
        {
            var audioPlayerMock = new Mock<AudioPlayer>();

            var songMock = new Mock<Song>("TestPath", AudioType.Mp3, TimeSpan.Zero);
            songMock.Setup(p => p.CreateAudioPlayer()).Returns(audioPlayerMock.Object);

            var library = new Library.Library();

            library.AddSongsToPlaylist(new[] { songMock.Object });

            library.PlaySong(0);

            library.RemoveFromPlaylist(new[] { 0 });

            audioPlayerMock.Verify(p => p.Stop(), Times.Once());

            library.Dispose();
        }
    }
}