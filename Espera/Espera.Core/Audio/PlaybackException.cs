using System;

namespace Espera.Core.Audio
{
    /// <summary>
    /// The exception that is thrown, when the <see cref="LocalAudioPlayer"/> couldn't play a song.
    /// </summary>
    [Serializable]
    public sealed class PlaybackException : Exception
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