using Espera.Core.Audio;
using System;
using System.Threading;

namespace Espera.Core.Tests.Mocks
{
    /// <summary>
    /// A <see cref="AudioPlayer"/> mock that sets the <see cref="AudioPlayer.PlaybackState"/> to <see cref="AudioPlayerState.Finished"/>
    /// when the <see cref="ManualResetEvent"/> is set, after <see cref="Play"/> is called.
    /// </summary>
    internal class HandledAudioPlayer : AudioPlayer
    {
        private readonly ManualResetEvent handle;

        public HandledAudioPlayer(ManualResetEvent handle)
        {
            this.handle = handle;
        }

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
            throw new NotImplementedException();
        }

        public override void Play()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Playing;

            handle.WaitOne();

            this.Finish();
        }
    }
}