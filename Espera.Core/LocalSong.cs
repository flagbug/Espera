using System.Reactive.Linq;
using Espera.Network;
using Rareform.Validation;
using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using TagLib;

namespace Espera.Core
{
    public sealed class LocalSong : Song
    {
        private readonly BehaviorSubject<string> artworkKey;

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

            this.artworkKey = new BehaviorSubject<string>(artworkKey);
        }

        /// <summary>
        /// Gets the key of the artwork for Akavache to retrieve. Null, if there is no album cover.
        /// </summary>
        public IObservable<string> ArtworkKey
        {
            get { return this.artworkKey.AsObservable(); }
        }

        /// <summary>
        /// Gets the key of the artwork for Akavache to retrieve. Null, if there is no album cover.
        ///
        /// This property ensures fast access without change notification.
        /// </summary>
        public string ArtworkKeyProperty
        {
            get { return this.artworkKey.Value; }
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

        public async Task SaveTagsToDisk()
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

            // Notify that all of the song metadata has changed, even if may not be really true, we
            // just want to update the UI
            this.OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// Notifies the <see cref="LocalSong" /> that the artwork has been stored to the permanent storage.
        /// </summary>
        /// <param name="key">The key of the artwork for Akavache to be retrieved.</param>
        internal void NotifyArtworkStored(string key)
        {
            this.artworkKey.OnNext(key);
        }
    }
}