using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Espera.Core.Audio;
using YoutubeExtractor;

namespace Espera.Core
{
    public sealed class YoutubeSong : Song
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="YoutubeSong"/> class.
        /// </summary>
        /// <param name="path">The path of the song.</param>
        /// <param name="audioType">The audio type.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <param name="isStreaming">if set to true, the song streams from YouTube, instead of downloading.</param>
        /// <exception cref="ArgumentNullException"><c>path</c> is null.</exception>
        public YoutubeSong(string path, AudioType audioType, TimeSpan duration, bool isStreaming)
            : base(path, audioType, duration)
        {
            this.IsStreaming = isStreaming;
        }

        /// <summary>
        /// Gets or sets the description that was entered by the uploader of the YouTube video.
        /// </summary>
        /// <value>
        /// The description that was entered by the uploader of the YouTube video.
        /// </value>
        public string Description { get; set; }

        public override bool HasToCache
        {
            get { return !this.IsStreaming; }
        }

        public bool IsStreaming { get; private set; }

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

        internal override AudioPlayer CreateAudioPlayer()
        {
            return this.IsStreaming ? (AudioPlayer)new YoutubeAudioPlayer() : new LocalAudioPlayer();
        }

        public override void LoadToCache()
        {
            this.IsCaching = true;

            string tempPath = Path.GetTempFileName();

            IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(this.OriginalPath);

            VideoInfo video = videoInfos
                .Where(info => info.CanExtractAudio)
                .First(info =>
                        info.VideoFormat == VideoFormat.FlashMp3HighQuality ||
                        info.VideoFormat == VideoFormat.FlashMp3LowQuality);

            var downloader = new AudioDownloader(video, tempPath);

            downloader.ProgressChanged += (sender, args) =>
            {
                this.CachingProgress = (int)args.ProgressPercentage;
            };

            downloader.Execute();
            this.StreamingPath = tempPath;

            this.IsCached = true;
        }
    }
}