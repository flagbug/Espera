using Espera.Core.Audio;
using Rareform.IO;
using System;
using System.IO;
using System.Linq;

namespace Espera.Core
{
    public sealed class LocalSong : Song
    {
        private bool? hasToCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSong"/> class.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="audioType">The audio type.</param>
        /// <param name="duration">The duration of the song.</param>
        public LocalSong(string path, AudioType audioType, TimeSpan duration)
            : base(path, audioType, duration)
        { }

        public override bool HasToCache
        {
            get
            {
                if (this.hasToCache == null)
                {
                    string songDrive = Path.GetPathRoot(this.OriginalPath);

                    this.hasToCache = DriveInfo.GetDrives()
                        .Where(drive => drive.DriveType == DriveType.Fixed)
                        .All(drive => drive.RootDirectory.Name != songDrive);
                }

                return this.hasToCache.Value;
            }
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
                this.OnCachingFailed();
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