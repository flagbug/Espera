using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;

namespace Espera.Core.Audio
{
    /// <summary>
    /// This class implements the basic audio player behavior. The actual playback implementation is
    /// defined by the setters of <see cref="IAudioPlayerCallback" /> (in this case a media element)
    /// </summary>
    public sealed class AudioPlayer : IAudioPlayerCallback
    {
        private readonly Subject<TimeSpan> currentTimeChangedFromOuter;
        private readonly SemaphoreSlim gate;
        private readonly BehaviorSubject<Song> loadedSong;
        private readonly BehaviorSubject<AudioPlayerState> playbackState;

        internal AudioPlayer()
        {
            this.gate = new SemaphoreSlim(1, 1);

            this.playbackState = new BehaviorSubject<AudioPlayerState>(AudioPlayerState.None);
            this.PlaybackState = this.playbackState.DistinctUntilChanged();

            this.loadedSong = new BehaviorSubject<Song>(null);
            this.TotalTime = this.loadedSong.Select(x => x == null ? TimeSpan.Zero : x.Duration);

            this.currentTimeChangedFromOuter = new Subject<TimeSpan>();

            var conn = Observable.Interval(TimeSpan.FromMilliseconds(300), RxApp.TaskpoolScheduler)
                .CombineLatest(this.PlaybackState, (l, state) => state)
                .Where(x => x == AudioPlayerState.Playing)
                .Select(_ => this.CurrentTime)
                .Merge(this.currentTimeChangedFromOuter)
                .DistinctUntilChanged(x => x.TotalSeconds)
                .Publish(TimeSpan.Zero);
            conn.Connect();
            this.CurrentTimeChanged = conn;
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

        public Func<Uri, Task> LoadRequest { set; private get; }

        public Func<Task> PauseRequest { set; private get; }

        public IObservable<AudioPlayerState> PlaybackState { get; private set; }

        public Func<Task> PlayRequest { set; private get; }

        public Action<TimeSpan> SetTime { set; private get; }

        public Action<float> SetVolume { set; private get; }

        public Func<Task> StopRequest { set; private get; }

        public IObservable<TimeSpan> TotalTime { get; private set; }

        public float Volume
        {
            get { return this.GetVolume(); }
            set { this.SetVolume(value); }
        }

        public async Task Finished()
        {
            await this.gate.WaitAsync();

            await this.SetPlaybackStateAsync(AudioPlayerState.Finished);

            this.gate.Release();
        }

        /// <summary>
        /// Loads the specified song asynchronously into the audio player.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="song" /> is <c>null</c></exception>
        /// <exception cref="SongLoadException">An error occured while loading the song.</exception>
        internal async Task LoadAsync(Song song)
        {
            if (song == null)
                throw new ArgumentNullException("song");

            this.loadedSong.OnNext(song);

            try
            {
                await this.LoadRequest(new Uri(this.loadedSong.Value.PlaybackPath));
            }

            catch (Exception ex)
            {
                throw new SongLoadException("Could not load song", ex);
            }
        }

        internal async Task PauseAsync()
        {
            await this.gate.WaitAsync();

            await this.PauseRequest();
            await this.SetPlaybackStateAsync(AudioPlayerState.Paused);

            this.gate.Release();
        }

        /// <summary>
        /// Plays the loaded song asynchronously and sets the <see cref="PlaybackState" /> to <see
        /// cref="AudioPlayerState.Playing" />
        /// </summary>
        /// <exception cref="PlaybackException">An error occured while playing the song.</exception>
        internal async Task PlayAsync()
        {
            await this.gate.WaitAsync();

            try
            {
                await this.PlayRequest();
                await this.SetPlaybackStateAsync(AudioPlayerState.Playing);
            }

            catch (Exception ex)
            {
                throw new PlaybackException("Could not play song", ex);
            }

            finally
            {
                this.gate.Release();
            }
        }

        internal async Task StopAsync()
        {
            await this.gate.WaitAsync();

            await this.StopRequest();
            await this.SetPlaybackStateAsync(AudioPlayerState.Stopped);

            this.gate.Release();
        }

        private async Task SetPlaybackStateAsync(AudioPlayerState state)
        {
            var connection = this.playbackState.FirstAsync(x => x == state).ToTask();

            // This is a poor man's trampoline
            Task.Run(() => this.playbackState.OnNext(state));

            await connection;
        }
    }
}