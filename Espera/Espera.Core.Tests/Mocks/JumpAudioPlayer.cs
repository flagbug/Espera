using Espera.Core.Audio;
using System;

namespace Espera.Core.Tests.Mocks
{
    /// <summary>
    /// A <see cref="AudioPlayer"/> mock that sets the <see cref="AudioPlayer.PlaybackState"/> to <see cref="AudioPlayerState.Finished"/>
    /// immediately after the <see cref="AudioPlayer.Play"/> method is called.
    /// </summary>
    internal class JumpAudioPlayer : AudioPlayer
    {
        public override TimeSpan CurrentTime
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public override IObservable<TimeSpan> TotalTime
        {
            get { throw new NotImplementedException(); }
        }

        public override float Volume { get; set; }

        public override void Dispose()
        {
            this.Finish();
        }

        public override void Pause()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Paused;
        }

        public override void Play()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Playing;
            this.Finish();
        }
    }
}