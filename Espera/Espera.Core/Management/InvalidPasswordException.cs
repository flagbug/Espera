using System;

namespace Espera.Core.Management
{
    /// <summary>
    /// The exception that is thrown, when an invalid password has been entered.
    /// </summary>
    [Serializable]
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