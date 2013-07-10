using Espera.Core.Audio;
using Rareform.IO;
using System;
using System.IO;

namespace Espera.Core
{
    public sealed class LocalSong : Song
    {
        private readonly DriveType sourceDriveType;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSong"/> class.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="audioType">The audio type.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <param name="sourceDriveType">The drive type where the song comes from.</param>
        public LocalSong(string path, AudioType audioType, TimeSpan duration, DriveType sourceDriveType)
            : base(path, audioType, duration)
        {
            this.sourceDriveType = sourceDriveType;
        }

        public override bool HasToCache
        {
            get { return this.sourceDriveType != DriveType.Fixed && this.sourceDriveType != DriveType.Network; }
        }

        public override string StreamingPath
        {
            get { return this.HasToCache ? base.StreamingPath : this.OriginalPath; }
            protected set { base.StreamingPath = value; }
        }

        public override void LoadToCache()
        {
            this.IsCaching = true;

            try
            {
                this.LoadToTempFile();
                this.IsCached = true;
            }

            catch (IOException)
            {
                this.OnPreparationFailed(PreparationFailureCause.CachingFailed);
            }

            finally
            {
                this.IsCaching = false;
            }
        }

        internal override AudioPlayer CreateAudioPlayer()
        {
            return new LocalAudioPlayer(this);
        }

        private void LoadToTempFile()
        {
            string path = Path.GetTempFileName();

            using (Stream sourceStream = File.OpenRead(this.OriginalPath))
            {
                using (Stream targetStream = File.OpenWrite(path))
                {
                    var operation = new StreamCopyOperation(sourceStream, targetStream);

                    operation.CopyProgressChanged += (sender, e) =>
                        this.OnCachingProgressChanged((int)e.ProgressPercentage);

                    operation.Execute();
                }
            }

            this.StreamingPath = path;
        }
    }
}