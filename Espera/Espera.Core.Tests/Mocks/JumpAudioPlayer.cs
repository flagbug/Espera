using Espera.Core.Audio;
using System;
using System.Threading.Tasks;

namespace Espera.Core.Tests.Mocks
{
    /// <summary>
    /// A <see cref="AudioPlayer"/> mock that sets the <see cref="AudioPlayer.PlaybackState"/> to <see cref="AudioPlayerState.Finished"/>
    /// immediately after the <see cref="AudioPlayer.PlayAsync"/> method is called.
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
        }

        public override Task PauseAsync()
        {
            throw new NotImplementedException();
        }

        public override async Task PlayAsync()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Playing;

            await this.FinishAsync();
        }

        public override Task StopAsync()
        {
            this.PlaybackStateProperty.Value = AudioPlayerState.Stopped;

            return Task.Delay(0);
        }
    }
}