using Espera.Core.Audio;
using System;
using System.Threading.Tasks;

namespace Espera.Core.Tests.Mocks
{
    internal class SimpleAudioPlayer : AudioPlayer
    {
        public override TimeSpan CurrentTime { get; set; }

        public override IObservable<TimeSpan> TotalTime
        {
            get { throw new NotImplementedException(); }
        }

        public override float Volume { get; set; }

        public override void Dispose()
        {
        }

        public override Task PauseAsync()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Paused;

            return Task.Delay(0);
        }

        public override Task PlayAsync()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Playing;

            return Task.Delay(0);
        }

        public override Task StopAsync()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Stopped;

            return Task.Delay(0);
        }
    }
}