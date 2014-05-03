using System;

namespace Espera.Core
{
    public class ArtworkFetchException : Exception
    {
        public ArtworkFetchException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}