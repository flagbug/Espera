using Espera.Core.Audio;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public class LocalAudioPlayerTest
    {
        [Fact]
        public void ConstructorShouldThrowArgumentNullExceptionIfSongIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new LocalAudioPlayer(null));
        }

        [Fact]
        public void CurrentTimeShouldBeZeroIfNoSongIsLoaded()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock());

            Assert.Equal(TimeSpan.Zero, audioPlayer.CurrentTime);
        }

        [Fact]
        public async Task PlaybackStateShouldBeNoneIfNoSongIsLoaded()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock());

            Assert.Equal(AudioPlayerState.None, await audioPlayer.PlaybackState.FirstAsync());
        }

        [Fact]
        public async Task TotalTimeShouldBeZeroIfNoSongIsLoaded()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock());

            Assert.Equal(TimeSpan.Zero, await audioPlayer.TotalTime.FirstAsync());
        }

        [Fact]
        public void VolumeShouldBeSettedVolumeIfNoSongIsLoadedButVolumeIsSet()
        {
            var audioPlayer = new LocalAudioPlayer(Helpers.SetupSongMock()) { Volume = 0.5f };

            Assert.Equal(0.5f, audioPlayer.Volume);
        }
    }
}