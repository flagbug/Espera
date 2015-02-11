using Espera.Network;
using Rareform.Validation;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Espera.Core
{
    [DebuggerDisplay("{Artist}-{Album}-{Title}")]
    public abstract class Song : IEquatable<Song>, INotifyPropertyChanged
    {
        private bool isCorrupted;

        /// <summary>
        /// Initializes a new instance of the <see cref="Song" /> class.
        /// </summary>
        /// <param name="path">The path of the song.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <exception cref="ArgumentNullException"><c>path</c> is null.</exception>
        protected Song(string path, TimeSpan duration)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            this.OriginalPath = path;
            this.Duration = duration;

            this.Album = String.Empty;
            this.Artist = String.Empty;
            this.Genre = String.Empty;
            this.Title = String.Empty;
            this.Guid = Guid.NewGuid();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Album { get; set; }

        public string Artist { get; set; }

        public TimeSpan Duration { get; set; }

        public string Genre { get; set; }

        /// <summary>
        /// A runtime identifier for interaction with the mobile API.
        /// </summary>
        public Guid Guid { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the song is corrupted and can't be played.
        /// </summary>
        /// <value><c>true</c> if the song is corrupted; otherwise, <c>false</c> .</value>
        public bool IsCorrupted
        {
            get { return this.isCorrupted; }
            set
            {
                if (this.isCorrupted != value)
                {
                    this.isCorrupted = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public abstract bool IsVideo { get; }

        public abstract NetworkSongSource NetworkSongSource { get; }

        /// <summary>
        /// Gets the path of the song on the local filesystem, or in the internet.
        /// </summary>
        public string OriginalPath { get; protected set; }

        /// <summary>
        /// Gets the path to stream the audio from.
        /// </summary>
        public abstract string PlaybackPath { get; }

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
            return this.OriginalPath.GetHashCode();
        }

        public override string ToString()
        {
            return String.Format("Title: {0}, Artist: {1}, Path: {2}", this.Title, this.Artist, this.OriginalPath);
        }

        public bool UpdateMetadataFrom(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            if (this.OriginalPath != song.OriginalPath)
                Throw.ArgumentException("The original path of both songs must be the same", () => song);

            // NB: Wow this is dumb
            bool changed = false;

            if (this.Album != song.Album)
            {
                this.Album = song.Album;
                changed = true;
            }

            if (this.Artist != song.Artist)
            {
                this.Artist = song.Artist;
                changed = true;
            }

            if (this.Duration != song.Duration)
            {
                this.Duration = song.Duration;
                changed = true;
            }

            if (this.Genre != song.Genre)
            {
                this.Genre = song.Genre;
                changed = true;
            }

            if (this.Title != song.Title)
            {
                this.Title = song.Title;
                changed = true;
            }

            if (this.TrackNumber != song.TrackNumber)
            {
                this.TrackNumber = song.TrackNumber;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Prepares the song for playback.
        /// </summary>
        internal virtual Task PrepareAsync(YoutubeStreamingQuality qualityHint)
        {
            return Task.Delay(0);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;

            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}