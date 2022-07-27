using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Espera.Core;

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
            hasThumbnail = this.WhenAnyValue(x => x.Thumbnail)
                .Select(x => x != null)
                .ToProperty(this, x => x.HasThumbnail);

            // Wait for the opening of the context menu to download the YouTube information
            this.WhenAnyValue(x => x.IsContextMenuOpen)
                .FirstAsync(x => x)
                .SelectMany(_ => LoadContextMenu().ToObservable())
                .Subscribe();

            // We have to set a dummy here, so that we can connect the commands
            isDownloading = Observable.Never<bool>().ToProperty(this, x => x.IsDownloading);

            DownloadVideoCommand = ReactiveCommand.CreateAsyncTask(
                this.WhenAnyValue(x => x.IsDownloading).Select(x => !x),
                x => DownloadVideo((VideoInfo)x, downloadPathFunc()));

            DownloadAudioCommand = ReactiveCommand.CreateAsyncTask(
                this.WhenAnyValue(x => x.IsDownloading).Select(x => !x),
                x => DownloadAudio((VideoInfo)x, downloadPathFunc()));

            isDownloading = DownloadVideoCommand.IsExecuting
                .CombineLatest(DownloadAudioCommand.IsExecuting, (x1, x2) => x1 || x2)
                .ToProperty(this, x => x.IsDownloading);
        }

        public IEnumerable<VideoInfo> AudioToDownload
        {
            get => audioToDownload;
            private set => this.RaiseAndSetIfChanged(ref audioToDownload, value);
        }

        public string Description => ((YoutubeSong)Model).Description;

        public ReactiveCommand<Unit> DownloadAudioCommand { get; }

        public bool DownloadFailed
        {
            get => downloadFailed;
            set => this.RaiseAndSetIfChanged(ref downloadFailed, value);
        }

        public int DownloadProgress
        {
            get => downloadProgress;
            set => this.RaiseAndSetIfChanged(ref downloadProgress, value);
        }

        public ReactiveCommand<Unit> DownloadVideoCommand { get; }

        public bool HasThumbnail => hasThumbnail.Value;

        public bool IsContextMenuOpen
        {
            get => isContextMenuOpen;
            set => this.RaiseAndSetIfChanged(ref isContextMenuOpen, value);
        }

        public bool IsDownloading => isDownloading.Value;

        public bool IsLoadingContextMenu
        {
            get => isLoadingContextMenu;
            private set => this.RaiseAndSetIfChanged(ref isLoadingContextMenu, value);
        }

        public bool IsLoadingThumbnail
        {
            get => isLoadingThumbnail;
            private set => this.RaiseAndSetIfChanged(ref isLoadingThumbnail, value);
        }

        public double? Rating => ((YoutubeSong)Model).Rating;

        public ImageSource Thumbnail
        {
            get
            {
                if (thumbnail == null) GetThumbnailAsync();

                return thumbnail;
            }

            private set => this.RaiseAndSetIfChanged(ref thumbnail, value);
        }

        public IEnumerable<VideoInfo> VideosToDownload
        {
            get => videosToDownload;
            private set => this.RaiseAndSetIfChanged(ref videosToDownload, value);
        }

        public int ViewCount => ((YoutubeSong)Model).Views;

        public string Views => string.Format(NumberFormatInfo.InvariantInfo, "{0:N0}", ((YoutubeSong)Model).Views);

        public async Task LoadContextMenu()
        {
            IsLoadingContextMenu = true;

            var infos = new List<VideoInfo>(0);

            try
            {
                infos = await Task.Run(() => DownloadUrlResolver.GetDownloadUrls(Path, false).ToList());
            }

            catch (YoutubeParseException ex)
            {
                this.Log().ErrorException("Failed to load the available YouTube videos", ex);
            }

            VideosToDownload = infos.Where(x => x.AdaptiveType == AdaptiveType.None && x.VideoType != VideoType.Unknown)
                .OrderBy(x => x.VideoType)
                .ThenByDescending(x => x.Resolution)
                .ToList();
            AudioToDownload = infos.Where(x => x.CanExtractAudio).OrderByDescending(x => x.AudioBitrate).ToList();

            IsLoadingContextMenu = false;
        }

        private async Task DownloadAudio(VideoInfo videoInfo, string downloadPath)
        {
            await DownloadFromYoutube(videoInfo, () => YoutubeSong.DownloadAudioAsync(videoInfo, downloadPath,
                Observer.Create<double>(progress => DownloadProgress = (int)progress)));
        }

        private async Task DownloadFromYoutube(VideoInfo videoInfo, Func<Task> downloadFunction)
        {
            DownloadProgress = 0;
            DownloadFailed = false;

            try
            {
                await Task.Run(() => DownloadUrlResolver.DecryptDownloadUrl(videoInfo));
            }

            catch (YoutubeParseException)
            {
                DownloadFailed = true;
                return;
            }

            try
            {
                await downloadFunction();
            }

            catch (YoutubeDownloadException)
            {
                DownloadFailed = true;
            }
        }

        private async Task DownloadVideo(VideoInfo videoInfo, string downloadPath)
        {
            await DownloadFromYoutube(videoInfo, () => YoutubeSong.DownloadVideoAsync(videoInfo, downloadPath,
                Observer.Create<double>(progress => DownloadProgress = (int)progress)));
        }

        private async Task GetThumbnailAsync()
        {
            var thumbnailUrl = ((YoutubeSong)Model).ThumbnailSource;
            thumbnailUrl = new Uri(thumbnailUrl.ToString().Replace("default", "hqdefault"));

            IsLoadingThumbnail = true;

            try
            {
                var image = await BlobCache.LocalMachine.LoadImageFromUrl(thumbnailUrl.ToString(),
                    absoluteExpiration: DateTimeOffset.Now + TimeSpan.FromMinutes(60));

                Thumbnail = image.ToNative();
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to download YouTube artwork", ex);
            }

            IsLoadingThumbnail = false;
        }
    }
}