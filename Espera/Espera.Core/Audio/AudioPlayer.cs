using System;
using Rareform.Extensions;

namespace Espera.Core.Audio
{
    internal abstract class AudioPlayer : IDisposable
    {
        /// <summary>
        /// Occurs when the <see cref="Song"/> has finished it's playback.
        /// </summary>
        public event EventHandler SongFinished;

        /// <summary>
        /// Gets or sets the current time.
        /// </summary>
        /// <value>The current time.</value>
        public abstract TimeSpan CurrentTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="AudioPlayer"/> has loaded the song.
        /// </summary>
        /// <value><c>true</c> if  the <see cref="AudioPlayer"/> has loaded the song.; otherwise, <c>false</c>.</value>
        public bool IsLoaded { get; protected set; }

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        /// <value>
        /// The current playback state.
        /// </value>
        public abstract AudioPlayerState PlaybackState { get; }

        /// <summary>
        /// Gets the song that the <see cref="AudioPlayer"/> is assigned to.
        /// </summary>
        /// <value>The song that the <see cref="AudioPlayer"/> is assigned to.</value>
        public Song Song { get; protected set; }

        /// <summary>
        /// Gets the total time.
        /// </summary>
        /// <value>The total time.</value>
        public abstract TimeSpan TotalTime { get; }

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
        {
            this.IsLoaded = true;
        }

        /// <summary>
        /// Pauses the playback of the <see cref="Song"/>.
        /// </summary>
        public abstract void Pause();

        /// <summary>
        /// Starts or continues the playback of the <see cref="Song"/>.
        /// </summary>
        /// <exception cref="PlaybackException">The playback couldn't be started.</exception>
        public abstract void Play();

        /// <summary>
        /// Stops the playback of the <see cref="Song"/>.
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Raises the <see cref="SongFinished"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected virtual void OnSongFinished(EventArgs e)
        {
            this.SongFinished.RaiseSafe(this, e);
        }
    }
}