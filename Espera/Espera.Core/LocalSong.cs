using System;
using Espera.Core.Audio;

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
        public LocalSong(Uri path, AudioType audioType, TimeSpan duration)
            : base(path, audioType, duration)
        { }
    }
}