using System;

namespace Espera.Core.Management
{
    /// <summary>
    /// The exception that is thrown, when the password to access the administrator mode is wrong.
    /// </summary>
    [Serializable]
    public sealed class WrongPasswordException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WrongPasswordException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public WrongPasswordException(string message)
            : base(message)
        { }
    }
}