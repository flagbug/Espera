using Espera.Core.Audio;
using System;

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

        public override void Pause()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Paused;
        }

        public override void Play()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Playing;
        }

        public override void Stop()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Stopped;
        }
    }
}