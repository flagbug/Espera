using System;
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
        ///     Initializes a new instance of the <see cref="Song" /> class.
        /// </summary>
        /// <param name="path">The path of the song.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <exception cref="ArgumentNullException"><c>path</c> is null.</exception>
        protected Song(string path, TimeSpan duration)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            OriginalPath = path;
            Duration = duration;

            Album = string.Empty;
            Artist = string.Empty;
            Genre = string.Empty;
            Title = string.Empty;
            Guid = Guid.NewGuid();
        }

        public string Album { get; set; }

        public string Artist { get; set; }

        public TimeSpan Duration { get; set; }

        public string Genre { get; set; }

        /// <summary>
        ///     A runtime identifier for interaction with the mobile API.
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        ///     Gets a value indicating whether the song is corrupted and can't be played.
        /// </summary>
        /// <value><c>true</c> if the song is corrupted; otherwise, <c>false</c> .</value>
        public bool IsCorrupted
        {
            get => isCorrupted;
            set
            {
                if (isCorrupted != value)
                {
                    isCorrupted = value;
                    OnPropertyChanged();
                }
            }
        }

        public abstract bool IsVideo { get; }

        public abstract NetworkSongSource NetworkSongSource { get; }

        /// <summary>
        ///     Gets the path of the song on the local filesystem, or in the internet.
        /// </summary>
        public string OriginalPath { get; protected set; }

        /// <summary>
        ///     Gets the path to stream the audio from.
        /// </summary>
        public abstract string PlaybackPath { get; }

        public string Title { get; set; }

        public int TrackNumber { get; set; }

        public bool Equals(Song other)
        {
            return other != null && OriginalPath == other.OriginalPath;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override bool Equals(object obj)
        {
            return Equals(obj as Song);
        }

        public override int GetHashCode()
        {
            return OriginalPath.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Title: {0}, Artist: {1}, Path: {2}", Title, Artist, OriginalPath);
        }

        public bool UpdateMetadataFrom(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            if (OriginalPath != song.OriginalPath)
                Throw.ArgumentException("The original path of both songs must be the same", () => song);

            // NB: Wow this is dumb
            var changed = false;

            if (Album != song.Album)
            {
                Album = song.Album;
                changed = true;
            }

            if (Artist != song.Artist)
            {
                Artist = song.Artist;
                changed = true;
            }

            if (Duration != song.Duration)
            {
                Duration = song.Duration;
                changed = true;
            }

            if (Genre != song.Genre)
            {
                Genre = song.Genre;
                changed = true;
            }

            if (Title != song.Title)
            {
                Title = song.Title;
                changed = true;
            }

            if (TrackNumber != song.TrackNumber)
            {
                TrackNumber = song.TrackNumber;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        ///     Prepares the song for playback.
        /// </summary>
        internal virtual Task PrepareAsync(YoutubeStreamingQuality qualityHint)
        {
            return Task.Delay(0);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;

            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}