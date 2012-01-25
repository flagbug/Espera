using System;

namespace FlagTunes.Core
{
    /// <summary>
    /// The exception that is thrown, when the <see cref="AudioPlayer"/> couldn't play a song.
    /// </summary>
    public class PlaybackException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public PlaybackException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}