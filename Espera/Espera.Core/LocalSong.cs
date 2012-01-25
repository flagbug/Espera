using System;
using System.IO;

namespace Espera.Core
{
    public class LocalSong : Song
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSong"/> class.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="audioType">The audio type.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <param name="dateAdded">The date when the song has been added.</param>
        public LocalSong(Uri path, AudioType audioType, TimeSpan duration, DateTime dateAdded)
            : base(path, audioType, duration, dateAdded)
        { }

        /// <summary>
        /// Opens a stream that can be used to play the song.
        /// </summary>
        /// <returns>
        /// A stream that can be used to play the song.
        /// </returns>
        internal override Stream OpenStream()
        {
            return File.OpenRead(this.Path.LocalPath);
        }
    }
}