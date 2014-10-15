using System;

namespace Espera.Core
{
    /// <summary>
    /// The exception that is thrown when the tags of a <see cref="LocalSong" /> couldn't be saved
    /// to the disk.
    /// </summary>
    public class TagsSaveException : Exception
    {
        public TagsSaveException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}