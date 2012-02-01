using System;

namespace Espera.Core
{
    public class InvalidPasswordException : Exception
    {
        public InvalidPasswordException(string message)
            : base(message)
        { }
    }
}