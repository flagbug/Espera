using System;

namespace Espera.Core
{
    public class NetworkSongFinderException : Exception
    {
        public NetworkSongFinderException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}