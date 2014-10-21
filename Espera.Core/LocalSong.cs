using Espera.Network;
using Rareform.Validation;
using System;
using System.Threading.Tasks;
using Espera.Core.Analytics;
using Splat;
using TagLib;

namespace Espera.Core
{
    public sealed class LocalSong : Song, IEnableLogger
    {
        private string artworkKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalSong" /> class.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <param name="artworkKey">
        /// The key of the artwork for Akavache to retrieve. Null, if there is no album cover or the
        /// artwork isn't retrieved yet.
        /// </param>
        public LocalSong(string path, TimeSpan duration, string artworkKey = null)
            : base(path, duration)
        {
            if (artworkKey == String.Empty)
                Throw.ArgumentException("Artwork key cannot be an empty string", () => artworkKey);

            this.ArtworkKey = artworkKey;
        }

        /// <summary>
        /// Gets the key of the artwork for Akavache to retrieve. Null, if there is no album cover.
        /// </summary>
        public string ArtworkKey
        {
            get { return this.artworkKey; }
            internal set
            {
                if (this.artworkKey != value)
                {
                    this.artworkKey = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public override bool IsVideo
        {
            get { return false; }
        }

        public override NetworkSongSource NetworkSongSource
        {
            get { return NetworkSongSource.Local; }
        }

        public override string PlaybackPath
        {
            get { return this.OriginalPath; }
        }

        /// <summary>
        /// Saves the metadata of this song to the disk.
        /// </summary>
        /// <exception cref="TagsSaveException">The saving of the metadata failed.</exception>
        public async Task SaveTagsToDisk()
        {
            try
            {
                using (var file = await Task.Run(() => File.Create(this.OriginalPath)))
                {
                    Tag tag = file.Tag;

                    tag.Album = this.Album;
                    tag.Performers = new[] { this.Artist };
                    tag.Genres = new[] { this.Genre };
                    tag.Title = this.Title;

                    await Task.Run(() => file.Save());
                }
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to save metadata for song " + this.OriginalPath, ex);

                AnalyticsClient.Instance.RecordNonFatalError(ex);

                throw new TagsSaveException("Failed to save tags", ex);
            }

            // Notify that all of the song metadata has changed, even if may not be really true, we
            // just want to update the UI
            this.OnPropertyChanged(string.Empty);
        }
    }
}