using System;
using System.IO;
using System.Linq;
using Espera.Core.Audio;
using Rareform.IO;

namespace Espera.Core
{
    public class LocalSong : Song
    {
        public bool IsRemovable
        {
            get
            {
                string songDrive = Path.GetPathRoot(this.OriginalPath.LocalPath);

                return DriveInfo.GetDrives()
                    .Where(drive => drive.DriveType == DriveType.Fixed)
                    .All(drive => drive.RootDirectory.Name != songDrive);
            }
        }

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
            if (this.IsRemovable)
            {
                string path = Path.GetTempFileName();

                using (Stream sourceStream = File.OpenRead(this.OriginalPath.LocalPath))
                {
                    using (Stream targetStream = File.OpenWrite(path))
                    {
                        var operation = new StreamCopyOperation(sourceStream, targetStream, 32 * 1024, true);

                        operation.CopyProgressChanged += (sender, e) => this.OnCachingProgressChanged(e);

                        operation.Execute();
                    }
                }

                this.StreamingPath = new Uri(path);
            }

            else
            {
                this.StreamingPath = this.OriginalPath;
            }

            this.IsCached = true;
        }
    }
}