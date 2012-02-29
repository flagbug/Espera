using System;
using System.IO;
using Espera.Core.Audio;
using Rareform.IO;

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

        internal override AudioPlayer CreateAudioPlayer()
        {
            return new LocalAudioPlayer();
        }

        internal override void LoadToCache()
        {
            string path = Path.GetTempFileName();

            var operation = new StreamCopyOperation(File.OpenRead(this.OriginalPath.LocalPath), File.OpenWrite(path), 32 * 1024, true);

            operation.CopyProgressChanged += (sender, e) => this.OnCachingProgressChanged(e);

            operation.Execute();

            this.StreamingPath = new Uri(path);

            this.IsCached = true;
        }
    }
}