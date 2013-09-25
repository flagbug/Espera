﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using YoutubeExtractor;

namespace Espera.Core
{
    public sealed class YoutubeSong : Song
    {
        private static readonly IReadOnlyDictionary<YoutubeStreamingQuality, IEnumerable<int>> StreamingQualityMap =
            new Dictionary<YoutubeStreamingQuality, IEnumerable<int>>
            {
                { YoutubeStreamingQuality.High, new HashSet<int> { 1080, 720 } },
                { YoutubeStreamingQuality.Medium, new HashSet<int> { 480 } },
                { YoutubeStreamingQuality.Low, new HashSet<int> { 360, 240 } }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="YoutubeSong"/> class.
        /// </summary>
        /// <param name="path">The path of the song.</param>
        /// <param name="duration">The duration of the song.</param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
        public YoutubeSong(string path, TimeSpan duration)
            : base(path, duration)
        { }

        /// <summary>
        /// Gets or sets the description that was entered by the uploader of the YouTube video.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the average rating.
        /// </summary>
        /// <value>
        /// The average rating. <c>null</c>, if the rating is unknown.
        /// </value>
        public double? Rating { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail source.
        /// </summary>
        public Uri ThumbnailSource { get; set; }

        /// <summary>
        /// Gets or sets the number of views for the YouTube video.
        /// </summary>
        public int Views { get; set; }

        public static async Task DownloadAudioAsync(VideoInfo videoInfo, IObserver<double> progress)
        {
            var downloader = new AudioDownloader(videoInfo, Path.Combine(CoreSettings.Default.YoutubeDownloadPath, videoInfo.Title + videoInfo.AudioExtension));

            downloader.DownloadProgressChanged += (sender, args) => progress.OnNext(args.ProgressPercentage * 0.95);
            downloader.AudioExtractionProgressChanged += (sender, args) => progress.OnNext(95 + args.ProgressPercentage * 0.05);

            await DownloadFromYoutube(downloader, new[] { typeof(IOException), typeof(WebException), typeof(AudioExtractionException) }, progress);
        }

        public static async Task DownloadVideoAsync(VideoInfo videoInfo, IObserver<double> progress)
        {
            var downloader = new VideoDownloader(videoInfo, Path.Combine(CoreSettings.Default.YoutubeDownloadPath, videoInfo.Title + videoInfo.VideoExtension));

            downloader.DownloadProgressChanged += (sender, args) => progress.OnNext(args.ProgressPercentage);

            await DownloadFromYoutube(downloader, new[] { typeof(IOException), typeof(WebException) }, progress);
        }

        internal override async Task PrepareAsync()
        {
            VideoInfo video = null;

            try
            {
                video = await GetVideoInfoForStreaming(this.OriginalPath);
            }

            catch (Exception ex)
            {
                if (ex is WebException || ex is VideoNotAvailableException || ex is YoutubeParseException)
                {
                    throw new SongPreparationException();
                }
            }

            if (video == null)
            {
                throw new SongPreparationException();
            }

            this.PlaybackPath = video.DownloadUrl;
        }

        private static async Task DownloadFromYoutube(Downloader downloader, IEnumerable<Type> exceptionTypes, IObserver<double> progress)
        {
            try
            {
                await Task.Run(() => downloader.Execute());
            }

            catch (Exception ex)
            {
                Exception outer = new YoutubeDownloadException("Youtube video or audio download failed", ex);

                if (exceptionTypes.Contains(ex.GetType()))
                {
                    progress.OnError(outer);
                }

                throw outer;
            }

            progress.OnCompleted();
        }

        private static VideoInfo GetVideoByStreamingQuality(IEnumerable<VideoInfo> videos, YoutubeStreamingQuality quality)
        {
            videos = videos.ToList(); // Prevent multiple enumeration

            if (CoreSettings.Default.StreamHighestYoutubeQuality)
            {
                return videos.OrderByDescending(x => x.Resolution)
                    .FirstOrDefault();
            }

            IEnumerable<int> preferredResolutions = StreamingQualityMap[quality];

            IEnumerable<VideoInfo> preferredVideos = videos
                .Where(info => preferredResolutions.Contains(info.Resolution))
                .OrderByDescending(info => info.Resolution);

            VideoInfo video = preferredVideos.FirstOrDefault();

            if (video == null)
            {
                return GetVideoByStreamingQuality(videos, (YoutubeStreamingQuality)(((int)quality) - 1));
            }

            return video;
        }

        private static async Task<VideoInfo> GetVideoInfoForStreaming(string youtubeLink)
        {
            IEnumerable<VideoInfo> videoInfos = await Task.Run(() => DownloadUrlResolver.GetDownloadUrls(youtubeLink));

            IEnumerable<VideoInfo> filtered = videoInfos
                .Where(info => info.VideoType == VideoType.Mp4 && !info.Is3D);

            return GetVideoByStreamingQuality(filtered, (YoutubeStreamingQuality)CoreSettings.Default.YoutubeStreamingQuality);
        }
    }
}