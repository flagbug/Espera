using System;
using Espera.Core.Audio;

namespace Espera.Core
{
    public class YoutubeSong : Song
    {
        /// <summary>
        /// Gets or sets the description that was entered by the uploader of the YouTube video.
        /// </summary>
        /// <value>
        /// The description that was entered by the uploader of the YouTube video.
        /// </value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the average rating.
        /// </summary>
        /// <value>
        /// The average rating.
        /// </value>
        public double Rating { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail source.
        /// </summary>
        /// <value>
        /// The thumbnail source.
        /// </value>
        public Uri ThumbnailSource { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="YoutubeSong"/> class.
        /// </summary>
        /// <param name="path">The path of the song.</param>
        /// <param name="audioType">The audio type.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <exception cref="ArgumentNullException"><c>path</c> is null.</exception>
        public YoutubeSong(string path, AudioType audioType, TimeSpan duration)
            : base(path, audioType, duration)
        { }

        internal override AudioPlayer CreateAudioPlayer()
        {
            return new YoutubeAudioPlayer();
        }

        internal override void LoadToCache()
        {
            this.StreamingPath = this.OriginalPath;
            this.IsCached = true;
            this.OnCachingCompleted(EventArgs.Empty);
        }

        internal override void ClearCache()
        {
            this.StreamingPath = null;
            this.IsCached = false;
        }
    }
}