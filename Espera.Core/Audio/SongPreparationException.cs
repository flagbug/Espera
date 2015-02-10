using System;

namespace Espera.Core
{
    public class SongPreparationException : Exception
    {
        public SongPreparationException(Exception innerException = null)
            : base("Error while preparing playback for song", innerException)
        { }

        public SongPreparationException(string message, Exception innerException = null)
            : base(message, innerException)
        { }
    }
}