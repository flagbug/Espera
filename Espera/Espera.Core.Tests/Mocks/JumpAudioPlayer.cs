using Espera.Core.Audio;
using System;

namespace Espera.Core.Tests.Mocks
{
    /// <summary>
    /// A <see cref="AudioPlayer"/> mock that raises the <see cref="AudioPlayer.SongFinished"/>
    /// event immediately when the <see cref="AudioPlayer.Play"/> method is called.
    /// </summary>
    internal class JumpAudioPlayer : AudioPlayer
    {
        public override TimeSpan CurrentTime
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public override AudioPlayerState PlaybackState
        {
            get { throw new NotImplementedException(); }
        }

        public override TimeSpan TotalTime
        {
            get { throw new NotImplementedException(); }
        }

        public override float Volume { get; set; }

        public override void Dispose()
        {
        }

        public override void Pause()
        {
            throw new NotImplementedException();
        }

        public override void Play()
        {
            this.OnSongFinished();
        }

        public override void Stop()
        {
            throw new NotImplementedException();
        }
    }
}