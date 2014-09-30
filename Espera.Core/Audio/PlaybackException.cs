using System;

namespace Espera.Core.Audio
{
    /// <summary>
    /// The exception that is thrown, when the <see cref="AudioPlayer"/> couldn't play a song.
    /// </summary>
    [Serializable]
    public sealed class PlaybackException : Exception
    {
        public PlaybackException()
        { }

        public PlaybackException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}