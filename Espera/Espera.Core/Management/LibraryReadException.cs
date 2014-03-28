using System;

namespace Espera.Core.Management
{
    public class LibraryReadException : Exception
    {
        public LibraryReadException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}