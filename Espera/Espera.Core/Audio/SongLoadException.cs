using System;

namespace Espera.Core.Audio
{
    /// <summary>
    /// The exception that is thrown, when the <see cref="AudioPlayer"/> couldn't load a song.
    /// </summary>
    [Serializable]
    public class SongLoadException : Exception
    {
        public SongLoadException(string message, Exception innerException)
            : base(message, innerException)
        { }

        public SongLoadException()
        { }
    }
}