using System;

namespace Espera.Core.Library
{
    /// <summary>
    /// The exception that is thrown, when an invalid password has been entered.
    /// </summary>
    public class InvalidPasswordException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidPasswordException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public InvalidPasswordException(string message)
            : base(message)
        { }
    }
}