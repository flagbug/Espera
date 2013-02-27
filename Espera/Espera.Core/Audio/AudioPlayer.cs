using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Espera.Core.Audio
{
    internal abstract class AudioPlayer : IDisposable
    {
        private readonly BehaviorSubject<AudioPlayerState> playbackState;
        private readonly Subject<Unit> songFinished;
        private readonly Subject<Unit> stopped;

        protected AudioPlayer()
        {
            this.songFinished = new Subject<Unit>();
            this.playbackState = new BehaviorSubject<AudioPlayerState>(AudioPlayerState.None);
            this.stopped = new Subject<Unit>();
        }

        /// <summary>
        /// Gets or sets the current time.
        /// </summary>
        /// <value>The current time.</value>
        public abstract TimeSpan CurrentTime { get; set; }

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        /// <value>
        /// The current playback state.
        /// </value>
        public virtual IObservable<AudioPlayerState> PlaybackState
        {
            get { return this.playbackState.AsObservable(); }
        }

        /// <summary>
        /// Gets the song that the <see cref="AudioPlayer"/> is assigned to.
        /// </summary>
        /// <value>The song that the <see cref="AudioPlayer"/> is assigned to.</value>
        public Song Song { get; protected set; }

        /// <summary>
        /// Occurs when the <see cref="Song"/> has finished it's playback.
        /// </summary>
        public IObservable<Unit> SongFinished
        {
            get { return this.songFinished.AsObservable(); }
        }

        public IObservable<Unit> Stopped
        {
            get { return this.stopped.AsObservable(); }
        }

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

        public abstract void Dispose();

        /// <summary>
        /// Loads the specified song into the <see cref="Espera.Core.Audio.LocalAudioPlayer"/>. This is required before playing a new song.
        /// </summary>
        /// <exception cref="SongLoadException">The song could not be loaded.</exception>
        public virtual void Load()
        { }

        /// <summary>
        /// Pauses the playback of the <see cref="Song"/>.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Paused"/>
        /// before leaving the method.
        /// This method must always be callable, even if the <see cref="AudioPlayer"/> isn't loaded, is stopped or is paused.
        /// In this case it shouldn't perform any operation.
        /// </remarks>
        public abstract void Pause();

        /// <summary>
        /// Starts or continues the playback of the <see cref="Song"/>.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Playing"/>
        /// before leaving the method.
        /// </remarks>
        /// <exception cref="PlaybackException">The playback couldn't be started.</exception>
        public abstract void Play();

        /// <summary>
        /// Stops the playback of the <see cref="Song"/>.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Stopped"/>
        /// before leaving the method.
        /// This method must always be callable, even if the <see cref="AudioPlayer"/> isn't loaded, is stopped or is paused.
        /// In this case it shouldn't perform any operation.
        /// </remarks>
        public abstract void Stop();

        protected void OnSongFinished()
        {
            this.songFinished.OnNext(Unit.Default);
        }

        protected void OnStopped()
        {
            this.stopped.OnNext(Unit.Default);
        }

        protected void SetPlaybackState(AudioPlayerState state)
        {
            this.playbackState.OnNext(state);
        }
    }
}