using Espera.Core.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using YoutubeExtractor;
using AudioType = Espera.Core.Audio.AudioType;

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
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        public YoutubeSong(string path, AudioType audioType, TimeSpan duration, bool isStreaming)
            : base(path, audioType, duration)
        {
            this.IsStreaming = isStreaming;
        }

        /// <summary>
        /// Gets or sets the description that was entered by the uploader of the YouTube video.
        /// </summary>
        public string Description { get; set; }

        public override bool HasToCache
        {
            get { return !this.IsStreaming; }
        }

        public bool IsStreaming { get; private set; }

        /// <summary>
        /// Gets or sets the average rating.
        /// </summary>
        public double Rating { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail source.
        /// </summary>
        public Uri ThumbnailSource { get; set; }

        /// <summary>
        /// Gets or sets the number of views for the YouTube video.
        /// </summary>
        public int Views { get; set; }

        public override void LoadToCache()
        {
            this.IsCaching = true;

            try
            {
                string tempPath = Path.GetTempFileName();

                VideoInfo video = GetVideoInfoForDownload(this.OriginalPath);

                this.DownloadVideo(video, tempPath);

                this.StreamingPath = tempPath;
                this.IsCached = true;
            }

            catch (IOException)
            {
                this.OnCachingFailed(EventArgs.Empty);
            }

            catch (WebException)
            {
                this.OnCachingFailed(EventArgs.Empty);
            }

            catch (VideoNotAvailableException)
            {
                this.OnCachingFailed(EventArgs.Empty);
            }

            catch (YoutubeParseException)
            {
                this.OnCachingFailed(EventArgs.Empty);
            }

            finally
            {
                this.IsCaching = false;
            }
        }

        internal override AudioPlayer CreateAudioPlayer()
        {
            if (this.IsStreaming)
            {
                VideoInfo video = GetVideoInfoForStreaming(this.OriginalPath);

                this.StreamingPath = video.DownloadUrl;
            }
            return this.IsStreaming ? (AudioPlayer)new YoutubeAudioPlayer(this) : new LocalAudioPlayer(this);
        }

        private static VideoInfo GetVideoInfoForDownload(string youtubeLink)
        {
            IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(youtubeLink);

            VideoInfo video = videoInfos
                .Where(info => info.CanExtractAudio && info.AudioType == YoutubeExtractor.AudioType.Mp3)
                .OrderByDescending(info => info.AudioBitrate)
                .First();

            return video;
        }

        private static VideoInfo GetVideoInfoForStreaming(string youtubeLink)
        {
            IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(youtubeLink);

            VideoInfo video = videoInfos
                .First(info => !info.Is3D && info.VideoType == VideoType.Mp4 && info.Resolution == 360);

            return video;
        }

        private void DownloadVideo(VideoInfo video, string tempPath)
        {
            var downloader = new AudioDownloader(video, tempPath);

            downloader.ProgressChanged += (sender, args) =>
            {
                this.CachingProgress = (int)args.ProgressPercentage;
            };

            downloader.Execute();
        }
    }
}