using System;

namespace Espera.Core.Management
{
    public class LibraryWriteException : Exception
    {
        public LibraryWriteException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}