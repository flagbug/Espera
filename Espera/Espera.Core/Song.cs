using Rareform.Validation;
using ReactiveMarrow;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Espera.Core
{
    [DebuggerDisplay("{Artist}-{Album}-{Title}")]
    public abstract class Song : IEquatable<Song>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Song"/> class.
        /// </summary>
        /// <param name="path">The path of the song.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <exception cref="ArgumentNullException"><c>path</c> is null.</exception>
        protected Song(string path, TimeSpan duration)
        {
            if (path == null)
                Throw.ArgumentNullException(() => path);

            this.OriginalPath = path;
            this.PlaybackPath = path;
            this.Duration = duration;

            this.Album = String.Empty;
            this.Artist = String.Empty;
            this.Genre = String.Empty;
            this.Title = String.Empty;

            this.IsCorrupted = new ReactiveProperty<bool>();
        }

        public string Album { get; set; }

        public string Artist { get; set; }

        public TimeSpan Duration { get; private set; }

        public string Genre { get; set; }

        /// <summary>
        /// Gets a value indicating whether the song is corrupted and can't be played.
        /// </summary>
        /// <value><c>true</c> if the song is corrupted; otherwise, <c>false</c>.</value>
        public ReactiveProperty<bool> IsCorrupted { get; private set; }

        /// <summary>
        /// Gets the path of the song on the local filesystem, or in the internet.
        /// </summary>
        public string OriginalPath { get; private set; }

        /// <summary>
        /// Gets the path to stream the audio from.
        /// </summary>
        public string PlaybackPath { get; protected set; }

        public string Title { get; set; }

        public int TrackNumber { get; set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Song);
        }

        public bool Equals(Song other)
        {
            return other != null && this.OriginalPath == other.OriginalPath;
        }

        public override int GetHashCode()
        {
            return new { this.OriginalPath, this.Duration }.GetHashCode();
        }

        public override string ToString()
        {
            return String.Format("Title: {0}, Artist: {1}, Path: {2}", this.Title, this.Artist, this.OriginalPath);
        }

        /// <summary>
        /// Prepares the song for playback.
        /// </summary>
        internal virtual Task PrepareAsync()
        {
            return Task.Delay(0);
        }
    }
}