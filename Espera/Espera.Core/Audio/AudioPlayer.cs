using ReactiveMarrow;
using System;
using System.Reactive.Linq;

namespace Espera.Core.Audio
{
    internal abstract class AudioPlayer : IDisposable
    {
        protected AudioPlayer()
        {
            this.PlaybackStateProperty = new ReactiveProperty<AudioPlayerState>(AudioPlayerState.None);
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
        /// Stops the playback of the <see cref="Song"/>.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Finished"/>
        /// before leaving the method.
        /// This method must always be callable, even if the <see cref="AudioPlayer"/> isn't loaded, is stopped or is paused.
        /// In this case it shouldn't perform any operation.
        /// After this method is called, the <see cref="Play"/> and <see cref="Pause"/> methods have to throw an <see cref="InvalidOperationException"/> if they are called.
        /// </remarks>
        public virtual void Finish()
        {
            if (this.PlaybackStateProperty.Value != AudioPlayerState.Finished)
            {
                this.PlaybackStateProperty.Value = AudioPlayerState.Finished;
            }
        }

        /// <summary>
        /// Loads the specified song into the <see cref="Espera.Core.Audio.LocalAudioPlayer"/>.
        /// Override this if the <see cref="AudioPlayer"/> needs to initialize before playing a song.
        /// </summary>
        /// <exception cref="SongLoadException">The song could not be loaded.</exception>
        /// <exception cref="InvalidOperationException">The method is called more than once.</exception>
        public virtual void Load()
        {
            if (this.PlaybackStateProperty.Value != AudioPlayerState.None)
                throw new InvalidOperationException("Load was already called");
        }

        /// <summary>
        /// Pauses the playback of the <see cref="Song"/>.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Paused"/>
        /// before leaving the method.
        /// This method must always be callable, even if the <see cref="AudioPlayer"/> isn't loaded, is stopped or is paused.
        /// In this case it shouldn't perform any operation.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The method is called after <see cref="Finish"/> has been called.</exception>
        public abstract void Pause();

        /// <summary>
        /// Starts or continues the playback of the <see cref="Song"/>.
        /// </summary>
        /// <remarks>
        /// This method has to ensure that the <see cref="PlaybackState"/> is set to <see cref="AudioPlayerState.Playing"/>
        /// before leaving the method.
        /// </remarks>
        /// <exception cref="PlaybackException">The playback couldn't be started.</exception>
        /// <exception cref="InvalidOperationException">The method is called after <see cref="Finish"/> has been called.</exception>
        public abstract void Play();
    }
}