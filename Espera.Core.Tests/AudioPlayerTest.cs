using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Espera.Core.Audio;
using NSubstitute;
using ReactiveUI;
using Xunit;

namespace Espera.Core.Tests
{
    public class AudioPlayerTest
    {
        [Fact]
        public void CanGetCurrentTimeAfterConstruction()
        {
            var audioPlayer = new AudioPlayer();

            Assert.Equal(TimeSpan.Zero, audioPlayer.CurrentTime);
        }

        [Fact]
        public void CanSetVolumeAfterConstruction()
        {
            var audioPlayer = new AudioPlayer();
            audioPlayer.SetVolume(0);
        }

        [Fact]
        public async Task StopsCurrentMediaPlayerWhenSwitchingAndPlaying()
        {
            var streamProxy = Substitute.For<IHttpsProxyService>();
            streamProxy.GetProxiedUri(new Uri("https://foobar.com")).ReturnsForAnyArgs(x => x.Args().First());

            var audioPlayer = new AudioPlayer(streamProxy);

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

        public class TheLoadAsyncMethod
        {
            [Fact]
            public async Task DisposesCurrentAudioPlayerIfNewOneRegistered()
            {
                var streamProxy = Substitute.For<IHttpsProxyService>();
                streamProxy.GetProxiedUri(new Uri("https://foobar.com")).ReturnsForAnyArgs(x => x.Args().First());

                var audioPlayer = new AudioPlayer(streamProxy);

                var oldMediaPlayer = Substitute.For<IMediaPlayerCallback, IDisposable>();
                var newMediaPlayer = Substitute.For<IMediaPlayerCallback, IDisposable>();

                audioPlayer.RegisterAudioPlayerCallback(oldMediaPlayer);
                await audioPlayer.LoadAsync(Helpers.SetupSongMock());

                audioPlayer.RegisterAudioPlayerCallback(newMediaPlayer);

                ((IDisposable)oldMediaPlayer).DidNotReceive().Dispose();

                await audioPlayer.LoadAsync(Helpers.SetupSongMock());

                ((IDisposable)oldMediaPlayer).Received().Dispose();
            }

            [Fact]
            public async Task LoadsIntoAudioPlayerIfSongIsAudio()
            {
                var streamProxy = Substitute.For<IHttpsProxyService>();
                streamProxy.GetProxiedUri(new Uri("https://foobar.com")).ReturnsForAnyArgs(x => x.Args().First());

                var audioPlayer = new AudioPlayer(streamProxy);
                var mediaPlayerCallback = Substitute.For<IMediaPlayerCallback>();
                audioPlayer.RegisterAudioPlayerCallback(mediaPlayerCallback);

                var song = Helpers.SetupSongMock("C://", TimeSpan.Zero);
                song.IsVideo.Returns(false);

                await audioPlayer.LoadAsync(song);

                mediaPlayerCallback.ReceivedWithAnyArgs().LoadAsync(Arg.Any<Uri>());
            }

            [Fact]
            public async Task LoadsIntoVideoPlayerIfSongIsVideo()
            {
                var streamProxy = Substitute.For<IHttpsProxyService>();
                streamProxy.GetProxiedUri(new Uri("https://foobar.com")).ReturnsForAnyArgs(x => x.Args().First());

                var audioPlayer = new AudioPlayer(streamProxy);
                var mediaPlayerCallback = Substitute.For<IMediaPlayerCallback>();
                audioPlayer.RegisterVideoPlayerCallback(mediaPlayerCallback);

                var song = Helpers.SetupSongMock("C://", TimeSpan.Zero);
                song.IsVideo.Returns(true);

                await audioPlayer.LoadAsync(song);

                mediaPlayerCallback.ReceivedWithAnyArgs().LoadAsync(Arg.Any<Uri>());
            }

            [Fact]
            public async Task StopsCurrentPlayback()
            {
                var streamProxy = Substitute.For<IHttpsProxyService>();
                streamProxy.GetProxiedUri(new Uri("https://foobar.com")).ReturnsForAnyArgs(x => x.Args().First());

                var audioPlayer = new AudioPlayer(streamProxy);

                var states = audioPlayer.PlaybackState.CreateCollection();

                var mediaPlayer = Substitute.For<IMediaPlayerCallback>();
                mediaPlayer.Finished.Returns(Observable.Never<Unit>());
                audioPlayer.RegisterAudioPlayerCallback(mediaPlayer);

                await audioPlayer.LoadAsync(Helpers.SetupSongMock());
                await audioPlayer.PlayAsync();

                await audioPlayer.LoadAsync(Helpers.SetupSongMock());

                Assert.Equal(new[] { AudioPlayerState.None, AudioPlayerState.Stopped, AudioPlayerState.Playing, AudioPlayerState.Stopped }, states);
            }
        }

        public class TheRegisterAudioPlayerMethod
        {
            [Fact]
            public async Task DisposesDanglingAudioPlayer()
            {
                var streamProxy = Substitute.For<IHttpsProxyService>();
                streamProxy.GetProxiedUri(new Uri("https://foobar.com")).ReturnsForAnyArgs(x => x.Args().First());

                var audioPlayer = new AudioPlayer(streamProxy);
                var mediaPlayer = Substitute.For<IMediaPlayerCallback>();
                audioPlayer.RegisterAudioPlayerCallback(mediaPlayer);
                await audioPlayer.LoadAsync(Helpers.SetupSongMock());

                var danglingPlayer = Substitute.For<IMediaPlayerCallback, IDisposable>();

                audioPlayer.RegisterAudioPlayerCallback(danglingPlayer);

                var newPlayer = Substitute.For<IMediaPlayerCallback>();

                audioPlayer.RegisterAudioPlayerCallback(newPlayer);

                ((IDisposable)danglingPlayer).Received(1).Dispose();
            }
        }
    }
}