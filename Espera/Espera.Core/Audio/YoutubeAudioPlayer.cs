using Rareform.Validation;
using System;

namespace Espera.Core.Audio
{
    /// <summary>
    /// An <see cref="AudioPlayer"/> that streams songs from YouTube.
    /// </summary>
    internal sealed class YoutubeAudioPlayer : AudioPlayer, IVideoPlayerCallback
    {
        private bool isPaused;
        private bool isPlaying;
        private bool isStopped;
        private float volume;

        public YoutubeAudioPlayer(YoutubeSong song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.Song = song;
        }

        public override TimeSpan CurrentTime
        {
            get { return this.GetTime(); }
            set { this.SetTime(value); }
        }

        public Func<TimeSpan> GetTime { set; private get; }

        public Action LoadRequest { set; private get; }

        public Action PauseRequest { set; private get; }

        public override AudioPlayerState PlaybackState
        {
            get
            {
                if (this.isPlaying)
                    return AudioPlayerState.Playing;

                if (this.isPaused)
                    return AudioPlayerState.Paused;

                if (this.isStopped)
                    return AudioPlayerState.Stopped;

                return AudioPlayerState.None;
            }
        }

        public Action PlayRequest { set; private get; }

        public Action<TimeSpan> SetTime { set; private get; }

        public Action StopRequest { set; private get; }

        public override TimeSpan TotalTime
        {
            get { return this.Song.Duration; }
        }

        public Uri VideoUrl
        {
            get { return new Uri(this.Song.StreamingPath); }
        }

        public override float Volume
        {
            get { return this.volume; }
            set
            {
                this.volume = value;

                this.VolumeChangeRequest(this.volume);
            }
        }

        public Action<float> VolumeChangeRequest { set; private get; }

        public override void Dispose()
        {
            this.Stop();
        }

        public void Finished()
        {
            this.isPlaying = false;
            this.isStopped = true;

            this.OnSongFinished(EventArgs.Empty);
        }

        public override void Load()
        {
            this.LoadRequest();
        }

        public override void Pause()
        {
            this.PauseRequest();

            this.isPlaying = false;
            this.isPaused = true;
        }

        public override void Play()
        {
            this.PlayRequest();

            this.isPlaying = true;
            this.isPaused = false;
        }

        public override void Stop()
        {
            this.StopRequest();

            this.isStopped = true;
        }
    }
}