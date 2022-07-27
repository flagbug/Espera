using System;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    /// <summary>
    ///     This class implements the basic audio player behavior.
    ///     The actual playback implementation is defined in the <see cref="IMediaPlayerCallback" /> implementations.
    /// </summary>
    public sealed class AudioPlayer : IEnableLogger
    {
        private readonly Subject<TimeSpan> currentTimeChangedFromOuter;
        private readonly SerialDisposable finishSubscription;
        private readonly SemaphoreSlim gate;
        private readonly BehaviorSubject<Song> loadedSong;
        private readonly BehaviorSubject<AudioPlayerState> playbackState;
        private IMediaPlayerCallback audioPlayerCallback;
        private IMediaPlayerCallback currentCallback;
        private bool disposeCurrentAudioCallback;
        private IMediaPlayerCallback videoPlayerCallback;

        internal AudioPlayer()
        {
            audioPlayerCallback = new DummyMediaPlayerCallback();
            videoPlayerCallback = new DummyMediaPlayerCallback();
            currentCallback = new DummyMediaPlayerCallback();

            finishSubscription = new SerialDisposable();
            gate = new SemaphoreSlim(1, 1);

            playbackState = new BehaviorSubject<AudioPlayerState>(AudioPlayerState.None);
            PlaybackState = playbackState.DistinctUntilChanged();

            loadedSong = new BehaviorSubject<Song>(null);
            TotalTime = loadedSong.Select(x => x == null ? TimeSpan.Zero : x.Duration);

            currentTimeChangedFromOuter = new Subject<TimeSpan>();

            var conn = Observable.Interval(TimeSpan.FromMilliseconds(300), RxApp.TaskpoolScheduler)
                .CombineLatest(PlaybackState, (l, state) => state)
                .Where(x => x == AudioPlayerState.Playing)
                .Select(_ => CurrentTime)
                .Merge(currentTimeChangedFromOuter)
                .DistinctUntilChanged(x => x.TotalSeconds)
                .Publish(TimeSpan.Zero);
            conn.Connect();
            CurrentTimeChanged = conn;
        }

        public TimeSpan CurrentTime
        {
            get => currentCallback.CurrentTime;
            set
            {
                currentCallback.CurrentTime = value;
                currentTimeChangedFromOuter.OnNext(CurrentTime);
            }
        }

        public IObservable<TimeSpan> CurrentTimeChanged { get; }

        public IObservable<Song> LoadedSong => loadedSong.AsObservable();

        public IObservable<AudioPlayerState> PlaybackState { get; }

        public IObservable<TimeSpan> TotalTime { get; }

        public void RegisterAudioPlayerCallback(IMediaPlayerCallback audioPlayerCallback)
        {
            if (disposeCurrentAudioCallback && this.audioPlayerCallback is IDisposable)
            {
                ((IDisposable)this.audioPlayerCallback).Dispose();
                disposeCurrentAudioCallback = false;
            }

            this.audioPlayerCallback = audioPlayerCallback;
            disposeCurrentAudioCallback = true;
        }

        public void RegisterVideoPlayerCallback(IMediaPlayerCallback videoPlayerCallback)
        {
            this.videoPlayerCallback = videoPlayerCallback;
        }

        public void SetVolume(float volume)
        {
            if (volume < 0 || volume > 1)
                throw new ArgumentOutOfRangeException("volume");

            currentCallback.SetVolume(volume);
        }

        /// <summary>
        ///     Loads the specified song asynchronously into the audio player.
        /// </summary>
        /// <param name="song">The song to load and play.</param>
        /// <exception cref="ArgumentNullException"><paramref name="song" /> is <c>null</c></exception>
        /// <exception cref="SongLoadException">An error occured while loading the song.</exception>
        internal async Task LoadAsync(Song song)
        {
            if (song == null)
                throw new ArgumentNullException("song");

            await gate.WaitAsync();

            finishSubscription.Disposable = Disposable.Empty;

            try
            {
                await currentCallback.StopAsync();
                await SetPlaybackStateAsync(AudioPlayerState.Stopped);
            }

            // If the stop method throws an exception and we don't swallow it, we can never reassign
            // the current callback
            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to stop current media player callback " + currentCallback, ex);
            }

            if (loadedSong.Value != null && !loadedSong.Value.IsVideo && disposeCurrentAudioCallback &&
                currentCallback is IDisposable) ((IDisposable)currentCallback).Dispose();

            disposeCurrentAudioCallback = false;

            loadedSong.OnNext(song);

            currentCallback = song.IsVideo ? videoPlayerCallback : audioPlayerCallback;

            try
            {
                await currentCallback.LoadAsync(new Uri(loadedSong.Value.PlaybackPath));

                finishSubscription.Disposable = currentCallback.Finished.FirstAsync()
                    .SelectMany(_ => Finished().ToObservable())
                    .Subscribe();
            }

            catch (Exception ex)
            {
                throw new SongLoadException("Could not load song", ex);
            }

            finally
            {
                gate.Release();
            }
        }

        internal async Task PauseAsync()
        {
            await gate.WaitAsync();

            try
            {
                await currentCallback.PauseAsync();
                await SetPlaybackStateAsync(AudioPlayerState.Paused);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to pause song", ex);
                throw;
            }

            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        ///     Plays the loaded song asynchronously and sets the <see cref="PlaybackState" /> to
        ///     <see
        ///         cref="AudioPlayerState.Playing" />
        /// </summary>
        /// <exception cref="PlaybackException">An error occured while playing the song.</exception>
        internal async Task PlayAsync()
        {
            await gate.WaitAsync();

            try
            {
                await currentCallback.PlayAsync();
                await SetPlaybackStateAsync(AudioPlayerState.Playing);
            }

            catch (Exception ex)
            {
                throw new PlaybackException("Could not play song", ex);
            }

            finally
            {
                gate.Release();
            }
        }

        internal async Task StopAsync()
        {
            await gate.WaitAsync();

            try
            {
                await currentCallback.StopAsync();
                await SetPlaybackStateAsync(AudioPlayerState.Stopped);
            }

            finally
            {
                gate.Release();
            }
        }

        private async Task Finished()
        {
            await gate.WaitAsync();

            await SetPlaybackStateAsync(AudioPlayerState.Finished);

            gate.Release();
        }

        private async Task SetPlaybackStateAsync(AudioPlayerState state)
        {
            var connection = playbackState.FirstAsync(x => x == state).ToTask();

            // This is a poor man's trampoline
            Task.Run(() => playbackState.OnNext(state));

            await connection;
        }
    }
}