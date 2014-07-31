using System.Threading.Tasks;
using Espera.Core.Audio;
using NSubstitute;
using Xunit;

namespace Espera.Core.Tests
{
    public class AudioPlayerTest
    {
        [Fact]
        public async Task StopsCurrentMediaPlayerWhenSwitchingAndPlaying()
        {
            var audioPlayer = new AudioPlayer();

            var oldMediaPlayer = Substitute.For<IMediaPlayerCallback>();
            var newMediaPlayer = Substitute.For<IMediaPlayerCallback>();

            audioPlayer.RegisterAudioPlayerCallback(oldMediaPlayer);

            var song = Helpers.SetupSongMock();

            await audioPlayer.LoadAsync(song);
            await audioPlayer.PlayAsync();

            audioPlayer.RegisterAudioPlayerCallback(newMediaPlayer);

            var song2 = Helpers.SetupSongMock();

            await audioPlayer.LoadAsync(song2);
            await audioPlayer.PlayAsync();

            oldMediaPlayer.Received(1).StopAsync();
        }
    }
}