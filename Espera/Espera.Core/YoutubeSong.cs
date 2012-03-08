using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Espera.Core.Audio;
using Rareform.IO;
using YoutubeExtractor;

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
            return new LocalAudioPlayer();
        }

        internal override void LoadToCache()
        {
            string tempPath = Path.GetTempFileName();

            IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(this.OriginalPath);

            VideoInfo video = videoInfos
                .Where(info => info.CanExtractAudio)
                .First(info =>
                       info.VideoFormat == VideoFormat.FlashMp3HighQuality ||
                       info.VideoFormat == VideoFormat.FlashMp3LowQuality);

            var downloader = new AudioDownloader(video, tempPath);

            // HACK: We don't know the total amnd transferred bytes, so we fake them
            downloader.ProgressChanged += (sender, args) =>
            {
                if ((int)args.ProgressPercentage > 0)
                {
                    this.OnCachingProgressChanged(new DataTransferEventArgs(100, (int)args.ProgressPercentage));
                }
            };

            downloader.Execute();

            this.StreamingPath = tempPath;
            this.IsCached = true;
            this.OnCachingCompleted(EventArgs.Empty);
        }

        internal override void ClearCache()
        {
            if (File.Exists(this.StreamingPath))
            {
                File.Delete(this.StreamingPath);
            }

            this.StreamingPath = null;
            this.IsCached = false;
        }
    }
}