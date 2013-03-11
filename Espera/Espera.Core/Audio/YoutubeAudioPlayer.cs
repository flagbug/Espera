using Rareform.Validation;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Espera.Core.Audio
{
    /// <summary>
    /// An <see cref="AudioPlayer"/> that streams songs from YouTube.
    /// </summary>
    internal sealed class YoutubeAudioPlayer : AudioPlayer, IVideoPlayerCallback
    {
        private readonly BehaviorSubject<TimeSpan> totalTime;

        public YoutubeAudioPlayer(YoutubeSong song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.Song = song;

            this.totalTime = new BehaviorSubject<TimeSpan>(TimeSpan.Zero);
        }

        public override TimeSpan CurrentTime
        {
            get { return this.GetTime(); }
            set { this.SetTime(value); }
        }

        public Func<TimeSpan> GetTime { set; private get; }

        public Func<float> GetVolume { set; private get; }

        public Action LoadRequest { set; private get; }

        public Action PauseRequest { set; private get; }

        public Action PlayRequest { set; private get; }

        public Action<TimeSpan> SetTime { set; private get; }

        public Action<float> SetVolume { set; private get; }

        public Action StopRequest { set; private get; }

        public override IObservable<TimeSpan> TotalTime
        {
            get { return this.totalTime.AsObservable(); }
        }

        public Uri VideoUrl
        {
            get { return new Uri(this.Song.StreamingPath); }
        }

        public override float Volume
        {
            get { return this.GetVolume(); }
            set { this.SetVolume(value); }
        }

        public override void Dispose()
        {
            this.StopRequest();
        }

        public override void Stop()
        {
            this.StopRequest();

            base.Stop();
        }

        public void Finished()
        {
            this.Finish();
        }

        public override void Load()
        {
            base.Load();

            this.LoadRequest();

            this.totalTime.OnNext(this.Song.Duration);
        }

        public override void Pause()
        {
            if (this.PlaybackStateProperty.Value == AudioPlayerState.Finished || 
                this.PlaybackStateProperty.Value == AudioPlayerState.Stopped)
                throw new InvalidOperationException("Audio player has already finished playback");

            this.PauseRequest();

            this.PlaybackStateProperty.Value = AudioPlayerState.Paused;
        }

        public override void Play()
        {
            if (this.PlaybackStateProperty.Value == AudioPlayerState.Finished || 
                this.PlaybackStateProperty.Value == AudioPlayerState.Stopped)
                throw new InvalidOperationException("Audio player has already finished playback");

            this.PlayRequest();

            this.PlaybackStateProperty.Value = AudioPlayerState.Playing;
        }
    }
}