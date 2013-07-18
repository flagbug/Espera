using Espera.Core.Audio;
using Rareform.IO;
using System;
using System.IO;
using System.Threading.Tasks;

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
        /// <param name="albumCoverKey">The key of the album cover for Akavache to retrieve. Null, if there is no album cover.</param>
        public LocalSong(string path, AudioType audioType, TimeSpan duration, DriveType sourceDriveType, string albumCoverKey)
            : base(path, audioType, duration)
        {
            this.sourceDriveType = sourceDriveType;
            this.AlbumCoverKey = albumCoverKey;
        }

        /// <summary>
        /// Gets the key of the album cover for Akavache to retrieve. Null, if there is no album cover.
        /// </summary>
        public string AlbumCoverKey { get; private set; }

        public override bool HasToCache
        {
            get { return this.sourceDriveType != DriveType.Fixed && this.sourceDriveType != DriveType.Network; }
        }

        public override string StreamingPath
        {
            get { return this.HasToCache ? base.StreamingPath : this.OriginalPath; }
            protected set { base.StreamingPath = value; }
        }

        public override async Task LoadToCacheAsync()
        {
            this.IsCaching = true;

            try
            {
                await this.LoadToTempFileAsync();
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

        internal override async Task<AudioPlayer> CreateAudioPlayerAsync()
        {
            return await Task.FromResult(new LocalAudioPlayer(this));
        }

        private async Task LoadToTempFileAsync()
        {
            string path = Path.GetTempFileName();

            using (Stream sourceStream = File.OpenRead(this.OriginalPath))
            {
                using (Stream targetStream = File.OpenWrite(path))
                {
                    var operation = new StreamCopyOperation(sourceStream, targetStream);

                    operation.CopyProgressChanged += (sender, e) =>
                        this.OnCachingProgressChanged((int)e.ProgressPercentage);

                    await Task.Run(() => operation.Execute());
                }
            }

            this.StreamingPath = path;
        }
    }
}