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
    /// This class implements the basic audio player behavior.
    ///
    /// The actual playback implementation is defined in the <see cref="IMediaPlayerCallback" /> implementations.
    /// </summary>
    public sealed class AudioPlayer
    {
        private readonly BehaviorSubject<IMediaPlayerCallback> audioPlayerCallback;
        private readonly Subject<TimeSpan> currentTimeChangedFromOuter;
        private readonly SemaphoreSlim gate;
        private readonly BehaviorSubject<Song> loadedSong;
        private readonly BehaviorSubject<AudioPlayerState> playbackState;
        private readonly BehaviorSubject<IMediaPlayerCallback> videoPlayerCallback;
        private IMediaPlayerCallback currentCallback;

        internal AudioPlayer()
        {
            this.audioPlayerCallback = new BehaviorSubject<IMediaPlayerCallback>(new DummyMediaPlayerCallback());
            this.videoPlayerCallback = new BehaviorSubject<IMediaPlayerCallback>(new DummyMediaPlayerCallback());

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
            get { return this.currentCallback == null ? TimeSpan.Zero : this.currentCallback.CurrentTime; }
            set
            {
                this.currentCallback.CurrentTime = value;
                this.currentTimeChangedFromOuter.OnNext(this.CurrentTime);
            }
        }

        public IObservable<TimeSpan> CurrentTimeChanged { get; private set; }

        public IObservable<Song> LoadedSong
        {
            get { return this.loadedSong.AsObservable(); }
        }

        public IObservable<AudioPlayerState> PlaybackState { get; private set; }

        public IObservable<TimeSpan> TotalTime { get; private set; }

        public void RegisterAudioPlayerCallback(IMediaPlayerCallback audioPlayerCallback)
        {
            this.audioPlayerCallback.OnNext(audioPlayerCallback);
        }

        public void RegisterVideoPlayerCallback(IMediaPlayerCallback videoPlayerCallback)
        {
            this.videoPlayerCallback.OnNext(videoPlayerCallback);
        }

        public void SetVolume(float volume)
        {
            if (volume < 0 || volume > 1)
                throw new ArgumentOutOfRangeException("volume");

            this.currentCallback.SetVolume(volume);
        }

        /// <summary>
        /// Loads the specified song asynchronously into the audio player.
        /// </summary>
        /// <param name="song">The song to load and play.</param>
        /// <exception cref="ArgumentNullException"><paramref name="song" /> is <c>null</c></exception>
        /// <exception cref="SongLoadException">An error occured while loading the song.</exception>
        internal async Task LoadAsync(Song song)
        {
            if (song == null)
                throw new ArgumentNullException("song");

            this.loadedSong.OnNext(song);

            try
            {
                this.currentCallback = song.IsVideo ? this.videoPlayerCallback.Value : this.audioPlayerCallback.Value;

                await this.currentCallback.LoadAsync(new Uri(this.loadedSong.Value.PlaybackPath));

                this.currentCallback.Finished.FirstAsync().TakeUntil(this.loadedSong.Skip(1))
                    .SelectMany(_ => this.Finished().ToObservable())
                    .Subscribe();
            }

            catch (Exception ex)
            {
                throw new SongLoadException("Could not load song", ex);
            }
        }

        internal async Task PauseAsync()
        {
            await this.gate.WaitAsync();

            await this.currentCallback.PauseAsync();
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
                await this.currentCallback.PlayAsync();
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

            await this.currentCallback.StopAsync();
            await this.SetPlaybackStateAsync(AudioPlayerState.Stopped);

            this.gate.Release();
        }

        private async Task Finished()
        {
            await this.gate.WaitAsync();

            await this.SetPlaybackStateAsync(AudioPlayerState.Finished);

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