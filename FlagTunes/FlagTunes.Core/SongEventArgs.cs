using System;
using FlagLib.Reflection;

namespace FlagTunes.Core
{
    /// <summary>
    /// Provides data for the <see cref="SongFinder.SongFound"/> and <see cref="Library.SongAdded"/> events.
    /// </summary>
    public class SongEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the song that has been found.
        /// </summary>
        /// <value>The song that has been found.</value>
        public Song Song { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SongEventArgs"/> class.
        /// </summary>
        /// <param name="song">The song that has been found.</param>
        /// <exception cref="ArgumentNullException"><c>song</c> is null.</exception>
        public SongEventArgs(Song song)
        {
            if (song == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => song));

            this.Song = song;
        }
    }
}