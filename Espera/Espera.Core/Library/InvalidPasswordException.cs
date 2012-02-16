using System;

namespace Espera.Core.Library
{
    public class InvalidPasswordException : Exception
    {
        public InvalidPasswordException(string message)
            : base(message)
        { }
    }
}