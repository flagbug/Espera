using System;
using Rareform.Extensions;
using Rareform.Validation;

namespace Espera.Core.Audio
{
    internal abstract class AudioPlayer : IDisposable
    {
        /// <summary>
        /// Occurs when the <see cref="LoadedSong"/> has finished it's playback.
        /// </summary>
        public event EventHandler SongFinished;

        /// <summary>
        /// Gets or sets the current time.
        /// </summary>
        /// <value>The current time.</value>
        public abstract TimeSpan CurrentTime { get; set; }

        /// <summary>
        /// Gets the song that is currently loaded into the audio player.
        /// </summary>
        /// <value>The song that is currently loaded into the audio player.</value>
        public Song LoadedSong { get; protected set; }

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        /// <value>
        /// The current playback state.
        /// </value>
        public abstract AudioPlayerState PlaybackState { get; }

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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Loads the specified song into the <see cref="Espera.Core.Audio.LocalAudioPlayer"/>. This is required before playing a new song.
        /// </summary>
        /// <param name="song">The song to load into the player.</param>
        /// <exception cref="ArgumentNullException"><c>song</c> is null.</exception>
        public virtual void Load(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.LoadedSong = song;
        }

        /// <summary>
        /// Pauses the playback of the <see cref="LoadedSong"/>.
        /// </summary>
        public abstract void Pause();

        /// <summary>
        /// Starts or continues the playback of the <see cref="LoadedSong"/>.
        /// </summary>
        /// <exception cref="PlaybackException">The playback couldn't be started.</exception>
        public abstract void Play();

        /// <summary>
        /// Stops the playback of the <see cref="LoadedSong"/>.
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