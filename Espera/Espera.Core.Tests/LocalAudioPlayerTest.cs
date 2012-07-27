using System;
using Espera.Core.Audio;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class LocalAudioPlayerTest
    {
        [Test]
        public void CurrentTime_NoSongLoaded_ReturnsTimeSpandZero()
        {
            var audioPlayer = new LocalAudioPlayer();

            Assert.AreEqual(TimeSpan.Zero, audioPlayer.CurrentTime);
        }

        [Test]
        public void PlaybackState_NoSongLoaded_ReturnsNone()
        {
            var audioPlayer = new LocalAudioPlayer();

            Assert.AreEqual(AudioPlayerState.None, audioPlayer.PlaybackState);
        }

        [Test]
        public void TotalTime_NoSongLoaded_ReturnsTimeSpanZero()
        {
            var audioPlayer = new LocalAudioPlayer();

            Assert.AreEqual(TimeSpan.Zero, audioPlayer.TotalTime);
        }

        [Test]
        public void Volume_NoSongLoadedAndVolumeIsSet_ReturnsSettedVolume()
        {
            var audioPlayer = new LocalAudioPlayer { Volume = 0.5f };

            Assert.AreEqual(0.5f, audioPlayer.Volume);
        }

        [Test]
        public void Load_ArgumentIsNull_ThrowsArgumentNullException()
        {
            var audioPlayer = new LocalAudioPlayer();

            Assert.Throws<ArgumentNullException>(() => audioPlayer.Load(null));
        }
    }
}