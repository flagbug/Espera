using System;

namespace FlagTunes.Core
{
    /// <summary>
    /// Represents a song
    /// </summary>
    public class Song : IEquatable<Song>
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
        /// Gets the path of the song on the local harddrive or removable disk.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Gets the date, when the song has been added.
        /// </summary>
        public DateTime DateAdded { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Song"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="dateAdded">The date when the song has been added.</param>
        public Song(string path, DateTime dateAdded)
        {
            this.Path = path;
            this.DateAdded = dateAdded;

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
            return new { this.Path, this.DateAdded }.GetHashCode();
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