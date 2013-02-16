using Espera.Core.Audio;
using Rareform.Validation;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Espera.Core
{
    /// <summary>
    /// Represents a song
    /// </summary>
    [DebuggerDisplay("{Artist}-{Album}-{Title}")]
    public abstract class Song : IEquatable<Song>
    {
        private readonly Subject<Unit> corrupted;
        private readonly Subject<Unit> preparationCompleted;
        private readonly Subject<PreparationFailureCause> preparationFailed;
        private readonly Subject<int> preparationProgress;
        private bool isCached;
        private bool isCorrupted;

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

            this.preparationProgress = new Subject<int>();
            this.preparationCompleted = new Subject<Unit>();
            this.preparationFailed = new Subject<PreparationFailureCause>();
            this.corrupted = new Subject<Unit>();
        }

        public string Album { get; set; }

        public string Artist { get; set; }

        public AudioType AudioType { get; private set; }

        public IObservable<Unit> Corrupted
        {
            get { return this.corrupted.AsObservable(); }
        }

        public TimeSpan Duration { get; private set; }

        public string Genre { get; set; }

        /// <summary>
        /// Gets a value indicating whether the song has to be cached before playing.
        /// </summary>
        /// <value>
        /// true if the song has to be cached before playing; otherwise, false.
        /// </value>
        public abstract bool HasToCache { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the song is completely cached.
        /// </summary>
        /// <value>
        /// true if the song is completely cached; otherwise, false.
        /// </value>
        public bool IsCached
        {
            get { return this.isCached; }
            protected set
            {
                this.isCached = value;

                if (this.isCached)
                {
                    this.OnCachingProgressChanged(100);
                    this.IsCaching = false;
                    this.preparationCompleted.OnNext(Unit.Default);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the song is currently caching.
        /// </summary>
        /// <value>
        /// true if the song is currently caching; otherwise, false.
        /// </value>
        public bool IsCaching { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the song is corrupted and can't be played.
        /// </summary>
        /// <value><c>true</c> if the song is corrupted; otherwise, <c>false</c>.</value>
        public bool IsCorrupted
        {
            get { return this.isCorrupted; }
            internal set
            {
                this.isCorrupted = value;

                if (this.isCorrupted)
                {
                    this.corrupted.OnNext(Unit.Default);
                }
            }
        }

        /// <summary>
        /// Gets the path of the song on the local filesystem, or in the internet.
        /// </summary>
        public string OriginalPath { get; private set; }

        public IObservable<Unit> PreparationCompleted
        {
            get { return this.preparationCompleted.AsObservable(); }
        }

        public IObservable<PreparationFailureCause> PreparationFailed
        {
            get { return this.preparationFailed.AsObservable(); }
        }

        /// <summary>
        /// Gets the preparation progress in a range from 0 to 100.
        /// </summary>
        public IObservable<int> PreparationProgress
        {
            get { return this.preparationProgress.AsObservable(); }
        }

        /// <summary>
        /// Gets the path to stream the audio from.
        /// </summary>
        /// <value>
        /// The path to stream the audio from.
        /// </value>
        public virtual string StreamingPath { get; protected set; }

        public string Title { get; set; }

        public int TrackNumber { get; set; }

        /// <summary>
        /// Deletes the temporary cache file and resets all attributes that are associated with caching.
        /// </summary>
        public void ClearCache()
        {
            // Safety check, to avoid that our whole local library gets deleted :)
            if (!this.HasToCache || !this.IsCached)
                throw new InvalidOperationException("Song should not be deleted, as it is not in a cache!");

            if (File.Exists(this.StreamingPath))
            {
                File.Delete(this.StreamingPath);
            }

            this.StreamingPath = null;
            this.IsCached = false;
            this.OnCachingProgressChanged(0);
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

        /// <summary>
        /// Loads the songs to a cache and sets the <see cref="StreamingPath"/> property.
        /// </summary>
        public abstract void LoadToCache();

        /// <summary>
        /// Creates the audio player for the song.
        /// </summary>
        /// <returns>The audio player for playback.</returns>
        internal abstract AudioPlayer CreateAudioPlayer();

        protected void OnCachingFailed(PreparationFailureCause failureCause)
        {
            this.preparationFailed.OnNext(failureCause);
        }

        /// <summary>
        /// Sets the caching progress in a range from 0 to 100.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value was not from 0 to 100.</exception>
        protected void OnCachingProgressChanged(int value)
        {
            if (value < 0)
                Throw.ArgumentOutOfRangeException(() => value, 0);

            if (value > 100)
                Throw.ArgumentOutOfRangeException(() => value, 100);

            this.preparationProgress.OnNext(value);
        }
    }
}