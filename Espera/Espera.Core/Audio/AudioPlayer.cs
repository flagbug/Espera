using ReactiveMarrow;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    internal abstract class AudioPlayer : IDisposable
    {
        private readonly Subject<TimeSpan> currentTimeChangedFromOuter;

        protected AudioPlayer()
        {
            this.PlaybackStateProperty = new ReactiveProperty<AudioPlayerState>();

            this.currentTimeChangedFromOuter = new Subject<TimeSpan>();

            this.CurrentTimeChanged = Observable.Interval(TimeSpan.FromMilliseconds(300))
                .CombineLatest(this.PlaybackState, (l, state) => state)
                .Where(x => x == AudioPlayerState.Playing)
                .Select(x => this.CurrentTime)
                .Merge(this.currentTimeChangedFromOuter)
                .DistinctUntilChanged(x => x.TotalSeconds);
        }

        public virtual TimeSpan CurrentTime { get; set; }

        public IObservable<TimeSpan> CurrentTimeChanged { get; private set; }

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        /// <value>
        /// The current playback state.
        /// </value>
        public IObservable<AudioPlayerState> PlaybackState
        {
            get { return this.PlaybackStateProperty.AsObservable(); }
        }

        /// <summary>
        /// Gets the song that the <see cref="AudioPlayer"/> is assigned to.
        /// </summary>
        /// <value>The song that the <see cref="AudioPlayer"/> is assigned to.</value>
        public Song Song { get; protected set; }

        /// <summary>
        /// Gets the total time.
        /// </summary>
        /// <value>The total time.</value>
        public abstract IObservable<TimeSpan> TotalTime { get; }

        /// <summary>
        /// Gets or sets the volume (a value from 0.0 to 1.0).
        /// </summary>
        /// <value>The volume.</value>
        public abstract float Volume { get; set; }

        protected ReactiveProperty<AudioPlayerState> PlaybackStateProperty { get; private set; }

        public abstract void Dispose();

        /// <summary>
        /// Loads the specified song into the <see cref="Espera.Core.Audio.LocalAudioPlayer"/>.
        /// Override this if the <see cref="AudioPlayer"/> needs to initialize before playing a song.
        /// </summary>
        /// <exception cref="SongLoadException">The song could not be loaded.</exception>
        /// <exception cref="InvalidOperationException">The method was called more than once.</exception>
        public virtual Task LoadAsync()
        {
            if (this.PlaybackStateProperty.Value != AudioPlayerState.None)
                throw new InvalidOperationException("Load was already called");

            return Task.Delay(0);
        }

        /// <summary>
        /// Pauses the playback of the <see cref="Song"/>.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Paused"/>
        /// before leaving the method.
        /// This method must always be callable, even if the <see cref="AudioPlayer"/> isn't loaded or is paused.
        /// In this case it shouldn't perform any operation.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The method is called after <see cref="FinishAsync"/> or <see cref="StopAsync"/> has been called.</exception>
        public abstract Task PauseAsync();

        /// <summary>
        /// Starts or continues the playback of the <see cref="Song"/>.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Playing"/>
        /// before leaving the method.
        /// </remarks>
        /// This method must always be callable, even if the <see cref="AudioPlayer"/> isn't loaded or is playing.
        /// In this case it shouldn't perform any operation.
        /// <exception cref="PlaybackException">The playback couldn't be started.</exception>
        /// <exception cref="InvalidOperationException">The method is called after <see cref="FinishAsync"/> or <see cref="StopAsync"/> has been called.</exception>
        public abstract Task PlayAsync();

        /// <summary>
        /// Prematurely stops the playback of a song.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Stopped"/>
        /// before leaving the method.
        /// This method must always be callable, even if the <see cref="AudioPlayer"/> isn't loaded or is paused.
        /// In this case it shouldn't perform any operation.
        /// After this method is called, the <see cref="PlayAsync"/> and <see cref="PauseAsync"/> methods have to throw an <see cref="InvalidOperationException"/> if they are called.
        /// </remarks>
        public abstract Task StopAsync();

        protected void CurrentTimeSet()
        {
            this.currentTimeChangedFromOuter.OnNext(this.CurrentTime);
        }

        /// <summary>
        /// Finishes the playback of the <see cref="Song"/>. This method is called when a song has ended.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Finished"/>
        /// before leaving the method.
        /// This method must always be callable, even if the <see cref="AudioPlayer"/> isn't loaded, is stopped or is paused.
        /// In this case it shouldn't perform any operation.
        /// After this method is called, the <see cref="PlayAsync"/> and <see cref="PauseAsync"/> methods have to throw an <see cref="InvalidOperationException"/> if they are called.
        /// </remarks>
        protected virtual Task FinishAsync()
        {
            if (this.PlaybackStateProperty.Value != AudioPlayerState.Finished)
            {
                this.PlaybackStateProperty.Value = AudioPlayerState.Finished;
            }

            return Task.Delay(0);
        }
    }
}