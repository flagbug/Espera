using System.Reactive.Linq;
using Espera.Network;
using Rareform.Validation;
using System;
using System.Reactive.Subjects;

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

            this.Guid = Guid.NewGuid();
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

        /// <summary>
        /// A runtime identifier for interaction with the mobile API.
        /// </summary>
        public Guid Guid { get; private set; }

        public override bool IsVideo
        {
            get { return false; }
        }

        public override NetworkSongSource NetworkSongSource
        {
            get { return NetworkSongSource.Local; }
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