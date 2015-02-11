using ReactiveUI;
using Splat;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    /// <summary>
    /// This class implements the basic audio player behavior.
    /// 
    /// The actual playback implementation is defined in the <see cref="IMediaPlayerCallback" /> implementations.
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
            this.audioPlayerCallback = new DummyMediaPlayerCallback();
            this.videoPlayerCallback = new DummyMediaPlayerCallback();
            this.currentCallback = new DummyMediaPlayerCallback();

            this.finishSubscription = new SerialDisposable();
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
            get { return this.currentCallback.CurrentTime; }
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
            if (this.disposeCurrentAudioCallback && this.audioPlayerCallback is IDisposable)
            {
                ((IDisposable)this.audioPlayerCallback).Dispose();
                this.disposeCurrentAudioCallback = false;
            }

            this.audioPlayerCallback = audioPlayerCallback;
            this.disposeCurrentAudioCallback = true;
        }

        public void RegisterVideoPlayerCallback(IMediaPlayerCallback videoPlayerCallback)
        {
            this.videoPlayerCallback = videoPlayerCallback;
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

            await this.gate.WaitAsync();

            this.finishSubscription.Disposable = Disposable.Empty;

            try
            {
                await this.currentCallback.StopAsync();
                await this.SetPlaybackStateAsync(AudioPlayerState.Stopped);
            }

            // If the stop method throws an exception and we don't swallow it, we can never reassign
            // the current callback
            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to stop current media player callback " + this.currentCallback, ex);
            }

            if (this.loadedSong.Value != null && !this.loadedSong.Value.IsVideo && this.disposeCurrentAudioCallback && this.currentCallback is IDisposable)
            {
                ((IDisposable)this.currentCallback).Dispose();
            }

            this.disposeCurrentAudioCallback = false;

            this.loadedSong.OnNext(song);

            this.currentCallback = song.IsVideo ? this.videoPlayerCallback : this.audioPlayerCallback;

            try
            {
                await this.currentCallback.LoadAsync(new Uri(this.loadedSong.Value.PlaybackPath));

                this.finishSubscription.Disposable = this.currentCallback.Finished.FirstAsync()
                    .SelectMany(_ => this.Finished().ToObservable())
                    .Subscribe();
            }

            catch (Exception ex)
            {
                throw new SongLoadException("Could not load song", ex);
            }

            finally
            {
                this.gate.Release();
            }
        }

        internal async Task PauseAsync()
        {
            await this.gate.WaitAsync();

            try
            {
                await this.currentCallback.PauseAsync();
                await this.SetPlaybackStateAsync(AudioPlayerState.Paused);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to pause song", ex);
                throw;
            }

            finally
            {
                this.gate.Release();
            }
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

            try
            {
                await this.currentCallback.StopAsync();
                await this.SetPlaybackStateAsync(AudioPlayerState.Stopped);
            }

            finally
            {
                this.gate.Release();
            }
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