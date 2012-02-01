using System;
using System.IO;
using FlagLib.Reflection;

namespace Espera.Core
{
    /// <summary>
    /// Represents a song
    /// </summary>
    public abstract class Song : IEquatable<Song>
    {
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
        public Uri Path { get; private set; }

        /// <summary>
        /// Gets or sets the type of the audio.
        /// </summary>
        public AudioType AudioType { get; private set; }

        /// <summary>
        /// Gets the duration of the song.
        /// </summary>
        public TimeSpan Duration { get; private set; }

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

            this.Path = path;
            this.AudioType = audioType;
            this.Duration = duration;

            this.Album = String.Empty;
            this.Artist = String.Empty;
            this.Genre = String.Empty;
            this.Title = String.Empty;
        }

        /// <summary>
        /// Opens a stream that can be used to play the song.
        /// </summary>
        /// <returns>A stream that can be used to play the song.</returns>
        internal abstract Stream OpenStream();

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// true if the specified <see cref="System.Object"/> is equal to this instance; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Song))
                return false;

            var other = (Song)obj;

            return this.Path == other.Path;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            return new { this.Path, this.Duration, this.AudioType }.GetHashCode();
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
    }
}