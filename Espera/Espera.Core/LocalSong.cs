using Espera.Core.Audio;
using Rareform.IO;
using Rareform.Validation;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Espera.Core
{
    public sealed class LocalSong : Song
    {
        private readonly BehaviorSubject<string> artworkKey;
        private readonly DriveType sourceDriveType;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSong"/> class.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="audioType">The audio type.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <param name="sourceDriveType">The drive type where the song comes from.</param>
        /// <param name="artworkKey">The key of the artwork for Akavache to retrieve. Null, if there is no album cover or the artwork isn't retrieved yet.</param>
        public LocalSong(string path, AudioType audioType, TimeSpan duration, DriveType sourceDriveType, string artworkKey = null)
            : base(path, audioType, duration)
        {
            if (artworkKey == String.Empty)
                Throw.ArgumentException("Artwork key cannot be an empty string", () => artworkKey);

            this.sourceDriveType = sourceDriveType;
            this.artworkKey = new BehaviorSubject<string>(artworkKey);
        }

        /// <summary>
        /// Gets the key of the artwork for Akavache to retrieve. Null, if there is no album cover.
        /// </summary>
        public IObservable<string> ArtworkKey
        {
            get { return this.artworkKey.AsObservable(); }
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

        /// <summary>
        /// Notifies the <see cref="LocalSong"/> that the artwork has been stored to the permanent storage.
        /// </summary>
        /// <param name="key">The key of the artwork for Akavache to be retrieved.</param>
        internal void NotifyArtworkStored(string key)
        {
            this.artworkKey.OnNext(key);
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