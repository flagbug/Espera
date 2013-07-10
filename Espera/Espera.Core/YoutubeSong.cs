using Espera.Core.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using YoutubeExtractor;
using AudioType = Espera.Core.Audio.AudioType;

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

        private readonly bool isStreaming;

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
            this.isStreaming = isStreaming;
        }

        /// <summary>
        /// Gets or sets the description that was entered by the uploader of the YouTube video.
        /// </summary>
        public string Description { get; set; }

        public override bool HasToCache
        {
            get { return !this.isStreaming; }
        }

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

        public async Task DownloadAudioAsync(VideoInfo videoInfo, IObserver<double> progress)
        {
            var downloader = new AudioDownloader(videoInfo, Path.Combine(CoreSettings.Default.YoutubeDownloadPath, videoInfo.Title + videoInfo.AudioExtension));

            downloader.DownloadProgressChanged += (sender, args) => progress.OnNext(args.ProgressPercentage * 0.95);
            downloader.AudioExtractionProgressChanged += (sender, args) => progress.OnNext(95 + args.ProgressPercentage * 0.05);

            await DownloadFromYoutube(downloader, new[] { typeof(IOException), typeof(WebException), typeof(AudioExtractionException) }, progress);
        }

        public async Task DownloadVideoAsync(VideoInfo videoInfo, IObserver<double> progress)
        {
            var downloader = new VideoDownloader(videoInfo, Path.Combine(CoreSettings.Default.YoutubeDownloadPath, videoInfo.Title + videoInfo.VideoExtension));

            downloader.DownloadProgressChanged += (sender, args) => progress.OnNext(args.ProgressPercentage);

            await DownloadFromYoutube(downloader, new[] { typeof(IOException), typeof(WebException) }, progress);
        }

        public override void LoadToCache()
        {
            this.IsCaching = true;

            try
            {
                string tempPath = Path.GetTempFileName();

                VideoInfo video = GetVideoInfoForDownload(this.OriginalPath);

                this.DownloadAudioTrack(video, tempPath);

                this.StreamingPath = tempPath;
                this.IsCached = true;
            }

            catch (Exception ex)
            {
                if (ex is IOException || ex is WebException || ex is VideoNotAvailableException ||
                    ex is YoutubeParseException || ex is AudioExtractionException)
                {
                    this.OnPreparationFailed(PreparationFailureCause.CachingFailed);
                }

                else
                {
                    throw;
                }
            }

            finally
            {
                this.IsCaching = false;
            }
        }

        internal override AudioPlayer CreateAudioPlayer()
        {
            if (this.isStreaming)
            {
                VideoInfo video = null;

                try
                {
                    video = GetVideoInfoForStreaming(this.OriginalPath);
                }

                catch (Exception ex)
                {
                    if (ex is WebException || ex is VideoNotAvailableException || ex is YoutubeParseException)
                    {
                        this.OnPreparationFailed(PreparationFailureCause.StreamingFailed);

                        throw new AudioPlayerCreatingException();
                    }
                }

                if (video == null)
                {
                    this.OnPreparationFailed(PreparationFailureCause.StreamingFailed);

                    throw new AudioPlayerCreatingException();
                }

                this.StreamingPath = video.DownloadUrl;
            }

            return this.isStreaming ? (AudioPlayer)new YoutubeAudioPlayer(this) : new LocalAudioPlayer(this);
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

            IEnumerable<VideoInfo> filtered = videoInfos
                .Where(info => info.VideoType == VideoType.Mp4 && !info.Is3D);

            return GetVideoByStreamingQuality(filtered, (YoutubeStreamingQuality)CoreSettings.Default.YoutubeStreamingQuality);
        }

        private void DownloadAudioTrack(VideoInfo video, string tempPath)
        {
            var downloader = new AudioDownloader(video, tempPath);

            // We need a factor at which the downlaod progress is preferred to the audio extraction progress
            const double factor = 0.95;

            downloader.DownloadProgressChanged += (sender, args) =>
                this.OnCachingProgressChanged((int)(args.ProgressPercentage * factor));

            downloader.AudioExtractionProgressChanged += (sender, args) =>
                this.OnCachingProgressChanged((int)(factor * 100) + (int)(args.ProgressPercentage * (1 - factor)));

            downloader.Execute();
        }
    }
}