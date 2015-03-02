using Akavache;
using Espera.Core;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Media;
using YoutubeExtractor;
using ReactiveCommand = ReactiveUI.ReactiveCommand;

namespace Espera.View.ViewModels
{
    public sealed class YoutubeSongViewModel : SongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<bool> hasThumbnail;
        private readonly ObservableAsPropertyHelper<bool> isDownloading;
        private IEnumerable<VideoInfo> audioToDownload;
        private bool downloadFailed;
        private int downloadProgress;
        private bool isContextMenuOpen;
        private bool isLoadingContextMenu;
        private bool isLoadingThumbnail;
        private ImageSource thumbnail;
        private IEnumerable<VideoInfo> videosToDownload;

        public YoutubeSongViewModel(YoutubeSong wrapped, Func<string> downloadPathFunc)
            : base(wrapped)
        {
            this.hasThumbnail = this.WhenAnyValue(x => x.Thumbnail)
                .Select(x => x != null)
                .ToProperty(this, x => x.HasThumbnail);

            // Wait for the opening of the context menu to download the YouTube information
            this.WhenAnyValue(x => x.IsContextMenuOpen)
                .FirstAsync(x => x)
                .SelectMany(_ => this.LoadContextMenu().ToObservable())
                .Subscribe();

            // We have to set a dummy here, so that we can connect the commands
            this.isDownloading = Observable.Never<bool>().ToProperty(this, x => x.IsDownloading);

            this.DownloadVideoCommand = ReactiveCommand.CreateAsyncTask(this.WhenAnyValue(x => x.IsDownloading).Select(x => !x),
                x => this.DownloadVideo((VideoInfo)x, downloadPathFunc()));

            this.DownloadAudioCommand = ReactiveCommand.CreateAsyncTask(this.WhenAnyValue(x => x.IsDownloading).Select(x => !x),
                x => this.DownloadAudio((VideoInfo)x, downloadPathFunc()));

            this.isDownloading = this.DownloadVideoCommand.IsExecuting
                .CombineLatest(this.DownloadAudioCommand.IsExecuting, (x1, x2) => x1 || x2)
                .ToProperty(this, x => x.IsDownloading);
        }

        public IEnumerable<VideoInfo> AudioToDownload
        {
            get { return this.audioToDownload; }
            private set { this.RaiseAndSetIfChanged(ref this.audioToDownload, value); }
        }

        public string Description
        {
            get { return ((YoutubeSong)this.Model).Description; }
        }

        public ReactiveCommand<Unit> DownloadAudioCommand { get; private set; }

        public bool DownloadFailed
        {
            get { return this.downloadFailed; }
            set { this.RaiseAndSetIfChanged(ref this.downloadFailed, value); }
        }

        public int DownloadProgress
        {
            get { return this.downloadProgress; }
            set { this.RaiseAndSetIfChanged(ref this.downloadProgress, value); }
        }

        public ReactiveCommand<Unit> DownloadVideoCommand { get; private set; }

        public bool HasThumbnail
        {
            get { return this.hasThumbnail.Value; }
        }

        public bool IsContextMenuOpen
        {
            get { return this.isContextMenuOpen; }
            set { this.RaiseAndSetIfChanged(ref this.isContextMenuOpen, value); }
        }

        public bool IsDownloading
        {
            get { return this.isDownloading.Value; }
        }

        public bool IsLoadingContextMenu
        {
            get { return this.isLoadingContextMenu; }
            private set { this.RaiseAndSetIfChanged(ref this.isLoadingContextMenu, value); }
        }

        public bool IsLoadingThumbnail
        {
            get { return this.isLoadingThumbnail; }
            private set { this.RaiseAndSetIfChanged(ref this.isLoadingThumbnail, value); }
        }

        public double? Rating
        {
            get { return ((YoutubeSong)this.Model).Rating; }
        }

        public ImageSource Thumbnail
        {
            get
            {
                if (this.thumbnail == null)
                {
                    this.GetThumbnailAsync();
                }

                return this.thumbnail;
            }

            private set { this.RaiseAndSetIfChanged(ref this.thumbnail, value); }
        }

        public IEnumerable<VideoInfo> VideosToDownload
        {
            get { return this.videosToDownload; }
            private set { this.RaiseAndSetIfChanged(ref this.videosToDownload, value); }
        }

        public int ViewCount
        {
            get { return ((YoutubeSong)this.Model).Views; }
        }

        public string Views
        {
            get { return String.Format(NumberFormatInfo.InvariantInfo, "{0:N0}", ((YoutubeSong)this.Model).Views); }
        }

        public async Task LoadContextMenu()
        {
            this.IsLoadingContextMenu = true;

            var infos = new List<VideoInfo>(0);

            try
            {
                infos = await Task.Run(() => DownloadUrlResolver.GetDownloadUrls(this.Path, false).ToList());
            }

            catch (YoutubeParseException ex)
            {
                this.Log().ErrorException("Failed to load the available YouTube videos", ex);
            }

            catch (VideoNotAvailableException ex)
            {
                this.Log().ErrorException("Failed to load the available YouTube videos", ex);
            }

            catch (WebException ex)
            {
                this.Log().ErrorException("Failed to load the available YouTube videos", ex);
            }

            this.VideosToDownload = infos.Where(x => x.AdaptiveType == AdaptiveType.None && x.VideoType != VideoType.Unknown)
                .OrderBy(x => x.VideoType)
                .ThenByDescending(x => x.Resolution)
                .ToList();
            this.AudioToDownload = infos.Where(x => x.CanExtractAudio).OrderByDescending(x => x.AudioBitrate).ToList();

            this.IsLoadingContextMenu = false;
        }

        private async Task DownloadAudio(VideoInfo videoInfo, string downloadPath)
        {
            await this.DownloadFromYoutube(videoInfo, () => YoutubeSong.DownloadAudioAsync(videoInfo, downloadPath,
                Observer.Create<double>(progress => this.DownloadProgress = (int)progress)));
        }

        private async Task DownloadFromYoutube(VideoInfo videoInfo, Func<Task> downloadFunction)
        {
            this.DownloadProgress = 0;
            this.DownloadFailed = false;

            try
            {
                await Task.Run(() => DownloadUrlResolver.DecryptDownloadUrl(videoInfo));
            }

            catch (YoutubeParseException)
            {
                this.DownloadFailed = true;
                return;
            }

            try
            {
                await downloadFunction();
            }

            catch (YoutubeDownloadException)
            {
                this.DownloadFailed = true;
            }
        }

        private async Task DownloadVideo(VideoInfo videoInfo, string downloadPath)
        {
            await this.DownloadFromYoutube(videoInfo, () => YoutubeSong.DownloadVideoAsync(videoInfo, downloadPath,
                Observer.Create<double>(progress => this.DownloadProgress = (int)progress)));
        }

        private async Task GetThumbnailAsync()
        {
            Uri thumbnailUrl = ((YoutubeSong)this.Model).ThumbnailSource;
            thumbnailUrl = new Uri(thumbnailUrl.ToString().Replace("default", "hqdefault"));

            this.IsLoadingThumbnail = true;

            try
            {
                IBitmap image = await BlobCache.LocalMachine.LoadImageFromUrl(thumbnailUrl.ToString(), absoluteExpiration: DateTimeOffset.Now + TimeSpan.FromMinutes(60));

                this.Thumbnail = image.ToNative();
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to download YouTube artwork", ex);
            }

            this.IsLoadingThumbnail = false;
        }
    }
}