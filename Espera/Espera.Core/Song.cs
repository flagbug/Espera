using System;
using System.Diagnostics;
using Espera.Core.Audio;
using Rareform.Extensions;
using Rareform.IO;
using Rareform.Reflection;

namespace Espera.Core
{
    /// <summary>
    /// Represents a song
    /// </summary>
    [DebuggerDisplay("{Artist}-{Album}-{Title}")]
    public abstract class Song : IEquatable<Song>
    {
        public event EventHandler<DataTransferEventArgs> CachingProgressChanged;

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>
        /// The title.
        /// </value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the artist.
        /// </summary>
        /// <value>
        /// The artist.
        /// </value>
        public string Artist { get; set; }

        /// <summary>
        /// Gets or sets the album.
        /// </summary>
        /// <value>
        /// The album.
        /// </value>
        public string Album { get; set; }

        /// <summary>
        /// Gets or sets the genre.
        /// </summary>
        /// <value>
        /// The genre.
        /// </value>
        public string Genre { get; set; }

        /// <summary>
        /// Gets or sets the track number.
        /// </summary>
        /// <value>
        /// The track number.
        /// </value>
        public int TrackNumber { get; set; }

        /// <summary>
        /// Gets the path of the song on the local filesystem, or in the internet.
        /// </summary>
        public Uri OriginalPath { get; private set; }

        public Uri StreamingPath { get; protected set; }

        /// <summary>
        /// Gets or sets the type of the audio.
        /// </summary>
        public AudioType AudioType { get; private set; }

        /// <summary>
        /// Gets the duration of the song.
        /// </summary>
        public TimeSpan Duration { get; private set; }

        public bool IsCached { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Song"/> class.
        /// </summary>
        /// <param name="path">The path of the song.</param>
        /// <param name="audioType">The audio type.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <exception cref="ArgumentNullException"><c>path</c> is null.</exception>
        protected Song(Uri path, AudioType audioType, TimeSpan duration)
        {
            if (path == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => path));

            this.OriginalPath = path;
            this.AudioType = audioType;
            this.Duration = duration;

            this.Album = String.Empty;
            this.Artist = String.Empty;
            this.Genre = String.Empty;
            this.Title = String.Empty;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// true if the specified <see cref="System.Object"/> is equal to this instance; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            var other = (Song)obj;

            if (obj == null)
                return false;

            return this.OriginalPath == other.OriginalPath;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            return new { Path = this.OriginalPath, this.Duration, this.AudioType }.GetHashCode();
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        public bool Equals(Song other)
        {
            return this.Equals((object)other);
        }

        internal protected void OnCachingProgressChanged(DataTransferEventArgs e)
        {
            this.CachingProgressChanged.RaiseSafe(this, e);
        }

        internal abstract AudioPlayer CreateAudioPlayer();

        internal abstract void LoadToCache();
    }
}