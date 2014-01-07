using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    /// <summary>
    /// An <see cref="AudioPlayer"/> that streams songs from YouTube.
    /// </summary>
    public sealed class AudioPlayer : IAudioPlayerCallback
    {
        private readonly Subject<TimeSpan> currentTimeChangedFromOuter;
        private readonly BehaviorSubject<Song> loadedSong;
        private readonly BehaviorSubject<AudioPlayerState> playbackState;

        internal AudioPlayer()
        {
            this.playbackState = new BehaviorSubject<AudioPlayerState>(AudioPlayerState.None);
            this.PlaybackState = this.playbackState.DistinctUntilChanged();

            this.loadedSong = new BehaviorSubject<Song>(null);
            this.TotalTime = this.loadedSong.Select(x => x == null ? TimeSpan.Zero : x.Duration);

            this.currentTimeChangedFromOuter = new Subject<TimeSpan>();

            this.CurrentTimeChanged = Observable.Interval(TimeSpan.FromMilliseconds(300))
                .CombineLatest(this.PlaybackState, (l, state) => state)
                .Where(x => x == AudioPlayerState.Playing)
                .Select(x => this.CurrentTime)
                .Merge(this.currentTimeChangedFromOuter)
                .DistinctUntilChanged(x => x.TotalSeconds);
        }

        public TimeSpan CurrentTime
        {
            get { return this.GetTime(); }
            set
            {
                this.SetTime(value);
                this.currentTimeChangedFromOuter.OnNext(this.CurrentTime);
            }
        }

        public IObservable<TimeSpan> CurrentTimeChanged { get; private set; }

        public Func<TimeSpan> GetTime { set; private get; }

        public Func<float> GetVolume { set; private get; }

        public IObservable<Song> LoadedSong
        {
            get { return this.loadedSong.AsObservable(); }
        }

        public Action LoadRequest { set; private get; }

        public Uri Path
        {
            get { return new Uri(this.loadedSong.Value.PlaybackPath); }
        }

        public Action PauseRequest { set; private get; }

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        /// <value>
        /// The current playback state.
        /// </value>
        public IObservable<AudioPlayerState> PlaybackState { get; private set; }

        public Action PlayRequest { set; private get; }

        public Action<TimeSpan> SetTime { set; private get; }

        public Action<float> SetVolume { set; private get; }

        public Action StopRequest { set; private get; }

        public IObservable<TimeSpan> TotalTime { get; private set; }

        public float Volume
        {
            get { return this.GetVolume(); }
            set { this.SetVolume(value); }
        }

        public void Dispose()
        {
            this.StopRequest();
        }

        public void Finished()
        {
            this.playbackState.OnNext(AudioPlayerState.Finished);
        }

        internal async Task LoadAsync(Song song)
        {
            if (song == null)
                throw new ArgumentNullException("song");

            this.loadedSong.OnNext(song);

            try
            {
                await Task.Run(this.LoadRequest);
            }

            catch (Exception ex)
            {
                throw new SongLoadException("Could not load song", ex);
            }
        }

        internal async Task PauseAsync()
        {
            await Task.Run(this.PauseRequest);

            this.playbackState.OnNext(AudioPlayerState.Paused);
        }

        internal async Task PlayAsync()
        {
            try
            {
                await Task.Run(this.PlayRequest);
            }

            catch (Exception ex)
            {
                throw new PlaybackException("Could not play song", ex);
            }

            this.playbackState.OnNext(AudioPlayerState.Playing);
        }

        internal async Task StopAsync()
        {
            await Task.Run(this.StopRequest);

            this.playbackState.OnNext(AudioPlayerState.Stopped);
        }
    }
}