using System;
using System.Threading.Tasks;
using Espera.Core.Analytics;

namespace Espera.Core
{
    public sealed class LocalSong : Song, IEnableLogger
    {
        private string artworkKey;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LocalSong" /> class.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <param name="artworkKey">
        ///     The key of the artwork for Akavache to retrieve. Null, if there is no album cover or the
        ///     artwork isn't retrieved yet.
        /// </param>
        public LocalSong(string path, TimeSpan duration, string artworkKey = null)
            : base(path, duration)
        {
            if (artworkKey == string.Empty)
                Throw.ArgumentException("Artwork key cannot be an empty string", () => artworkKey);

            ArtworkKey = artworkKey;
        }

        /// <summary>
        ///     Gets the key of the artwork for Akavache to retrieve. Null, if there is no album cover.
        /// </summary>
        public string ArtworkKey
        {
            get => artworkKey;
            internal set
            {
                if (artworkKey != value)
                {
                    artworkKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public override bool IsVideo => false;

        public override NetworkSongSource NetworkSongSource => NetworkSongSource.Local;

        public override string PlaybackPath => OriginalPath;

        /// <summary>
        ///     Saves the metadata of this song to the disk.
        /// </summary>
        /// <exception cref="TagsSaveException">The saving of the metadata failed.</exception>
        public async Task SaveTagsToDisk()
        {
            try
            {
                using (var file = await Task.Run(() => File.Create(OriginalPath)))
                {
                    var tag = file.Tag;

                    tag.Album = Album;
                    tag.Performers = new[] { Artist };
                    tag.Genres = new[] { Genre };
                    tag.Title = Title;

                    await Task.Run(() => file.Save());
                }
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to save metadata for song " + OriginalPath, ex);

                AnalyticsClient.Instance.RecordNonFatalError(ex);

                throw new TagsSaveException("Failed to save tags", ex);
            }

            // Notify that all of the song metadata has changed, even if may not be really true, we
            // just want to update the UI
            OnPropertyChanged(string.Empty);
        }
    }
}