using Espera.Core.Audio;
using NUnit.Framework;
using System;
using System.Reactive.Linq;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class LocalAudioPlayerTest
    {
        [Test]
        public void Constructor_SongIsNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new LocalAudioPlayer(null));
        }

        [Test]
        public void CurrentTime_NoSongLoaded_ReturnsTimeSpandZero()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock());

            Assert.AreEqual(TimeSpan.Zero, audioPlayer.CurrentTime);
        }

        [Test]
        public void PlaybackState_NoSongLoaded_ReturnsNone()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock());

            Assert.AreEqual(AudioPlayerState.None, audioPlayer.PlaybackState.FirstAsync().Wait());
        }

        [Test]
        public void Stop_SongNotLoaded_PassesWithoutException()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock());

            audioPlayer.Stop();
        }

        [Test]
        public void TotalTime_NoSongLoaded_ReturnsTimeSpanZero()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock());

            Assert.AreEqual(TimeSpan.Zero, audioPlayer.TotalTime.FirstAsync().Wait());
        }

        [Test]
        public void Volume_NoSongLoadedAndVolumeIsSet_ReturnsSettedVolume()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock()) { Volume = 0.5f };

            Assert.AreEqual(0.5f, audioPlayer.Volume);
        }
    }
}