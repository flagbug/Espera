using System;
using System.Diagnostics;
using System.IO;
using Espera.Core.Audio;
using Rareform.Extensions;
using Rareform.Validation;

namespace Espera.Core
{
    /// <summary>
    /// Represents a song
    /// </summary>
    [DebuggerDisplay("{Artist}-{Album}-{Title}")]
    public abstract class Song : IEquatable<Song>
    {
        private string streamingPath;
        private bool isCached;

        /// <summary>
        /// Initializes a new instance of the <see cref="Song"/> class.
        /// </summary>
        /// <param name="path">The path of the song.</param>
        /// <param name="audioType">The audio type.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <exception cref="ArgumentNullException"><c>path</c> is null.</exception>
        protected Song(string path, AudioType audioType, TimeSpan duration)
        {
            if (path == null)
                Throw.ArgumentNullException(() => path);

            this.OriginalPath = path;
            this.AudioType = audioType;
            this.Duration = duration;

            this.Album = String.Empty;
            this.Artist = String.Empty;
            this.Genre = String.Empty;
            this.Title = String.Empty;
        }

        public event EventHandler CachingCompleted;

        public event EventHandler CachingFailed;

        public event EventHandler CachingProgressChanged;

        /// <summary>
        /// Gets or sets the album.
        /// </summary>
        /// <value>
        /// The album.
        /// </value>
        public string Album { get; set; }

        /// <summary>
        /// Gets or sets the artist.
        /// </summary>
        /// <value>
        /// The artist.
        /// </value>
        public string Artist { get; set; }

        /// <summary>
        /// Gets or sets the type of the audio.
        /// </summary>
        public AudioType AudioType { get; private set; }

        public int CachingProgress { get; protected set; }

        /// <summary>
        /// Gets the duration of the song.
        /// </summary>
        public TimeSpan Duration { get; private set; }

        /// <summary>
        /// Gets or sets the genre.
        /// </summary>
        /// <value>
        /// The genre.
        /// </value>
        public string Genre { get; set; }

        /// <summary>
        /// Gets a value indicating whether the song has to be cached before playing.
        /// </summary>
        /// <value>
        /// true if the song has to be cached before playing; otherwise, false.
        /// </value>
        public abstract bool HasToCache { get; }

        public bool IsCached
        {
            get { return this.isCached; }
            protected set
            {
                this.CachingProgress = 100;
                this.isCached = value;
            }
        }

        /// <summary>
        /// Gets the path of the song on the local filesystem, or in the internet.
        /// </summary>
        public string OriginalPath { get; private set; }

        public string StreamingPath
        {
            get { return !this.HasToCache ? this.OriginalPath : this.streamingPath; }
            protected set { this.streamingPath = value; }
        }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>
        /// The title.
        /// </value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the track number.
        /// </summary>
        /// <value>
        /// The track number.
        /// </value>
        public int TrackNumber { get; set; }

        public void ClearCache()
        {
            if (File.Exists(this.StreamingPath))
            {
                File.Delete(this.StreamingPath);
            }

            this.StreamingPath = null;
            this.IsCached = false;
        }

        public abstract AudioPlayer CreateAudioPlayer();

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// true if the specified <see cref="System.Object"/> is equal to this instance; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as Song);
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
            return other != null && this.OriginalPath == other.OriginalPath;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            return new { this.OriginalPath, this.Duration, this.AudioType }.GetHashCode();
        }

        public abstract void LoadToCache();

        internal protected void OnCachingFailed(EventArgs e)
        {
            this.CachingFailed.RaiseSafe(this, e);
        }

        internal protected void OnCachingCompleted(EventArgs e)
        {
            this.CachingCompleted.RaiseSafe(this, e);
        }

        internal protected void OnCachingProgressChanged(EventArgs e)
        {
            this.CachingProgressChanged.RaiseSafe(this, e);
        }
    }
}