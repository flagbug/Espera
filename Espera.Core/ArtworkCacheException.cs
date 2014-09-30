using System;

namespace Espera.Core
{
    public class ArtworkCacheException : Exception
    {
        public ArtworkCacheException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}