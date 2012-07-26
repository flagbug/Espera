using System;
using Rareform.Validation;

namespace Espera.Core
{
    /// <summary>
    /// Provides data for the <see cref="LocalSongFinder.SongFound"/> event.
    /// </summary>
    public sealed class SongEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SongEventArgs"/> class.
        /// </summary>
        /// <param name="song">The song that has been found.</param>
        /// <exception cref="ArgumentNullException"><c>song</c> is null.</exception>
        public SongEventArgs(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.Song = song;
        }

        /// <summary>
        /// Gets the song that has been found.
        /// </summary>
        /// <value>The song that has been found.</value>
        public Song Song { get; private set; }
    }
}