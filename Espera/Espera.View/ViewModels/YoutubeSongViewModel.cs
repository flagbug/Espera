using Espera.Core;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoutubeExtractor;

namespace Espera.View.ViewModels
{
    public sealed class YoutubeSongViewModel : SongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<bool> hasThumbnail;
        private readonly ObservableAsPropertyHelper<bool> isDownloading;
        private IEnumerable<VideoInfo> audioToDownload;
        private bool downloadFailed;
        private double downloadProgress;
        private bool isContextMenuOpen;
        private bool isLoadingContextMenu;
        private ImageSource thumbnail;
        private IEnumerable<VideoInfo> videosToDownload;

        public YoutubeSongViewModel(YoutubeSong wrapped, Func<string> downloadPathFunc)
            : base(wrapped)
        {
            this.OpenPathCommand = new ReactiveCommand();
            this.OpenPathCommand.Subscribe(x => Process.Start(this.Path));

            this.hasThumbnail = this.WhenAnyValue(x => x.Thumbnail)
                .Select(x => x != null)
                .ToProperty(this, x => x.HasThumbnail);

            // Wait for the opening of the context menu to download the YouTube information
            this.WhenAnyValue(x => x.IsContextMenuOpen)
                .FirstAsync(x => x)
                .Subscribe(_ => this.LoadContextMenu());

            // We have to set a dummy here, so that we can connect the commands
            this.isDownloading = Observable.Never<bool>().ToProperty(this, x => x.IsDownloading);

            this.DownloadVideoCommand = new ReactiveCommand(this.WhenAnyValue(x => x.IsDownloading).Select(x => !x));
            this.DownloadVideoCommand.RegisterAsyncTask(x => this.DownloadVideo((VideoInfo)x, downloadPathFunc()));

            this.DownloadAudioCommand = new ReactiveCommand(this.WhenAnyValue(x => x.IsDownloading).Select(x => !x));
            this.DownloadAudioCommand.RegisterAsyncTask(x => this.DownloadAudio((VideoInfo)x, downloadPathFunc()));

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

        public IReactiveCommand DownloadAudioCommand { get; private set; }

        public bool DownloadFailed
        {
            get { return this.downloadFailed; }
            set { this.RaiseAndSetIfChanged(ref this.downloadFailed, value); }
        }

        public double DownloadProgress
        {
            get { return this.downloadProgress; }
            set { this.RaiseAndSetIfChanged(ref this.downloadProgress, value); }
        }

        public IReactiveCommand DownloadVideoCommand { get; private set; }

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

        public IReactiveCommand OpenPathCommand { get; private set; }

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

            IEnumerable<VideoInfo> infos = await Task.Run(() => DownloadUrlResolver.GetDownloadUrls(this.Path).ToList());
            this.VideosToDownload = infos.OrderBy(x => x.VideoType).ThenByDescending(x => x.Resolution).ToList();
            this.AudioToDownload = infos.Where(x => x.CanExtractAudio).OrderByDescending(x => x.AudioBitrate).ToList();

            this.IsLoadingContextMenu = false;
        }

        private async Task DownloadAudio(VideoInfo videoInfo, string downloadPath)
        {
            await this.DownloadFromYoutube(() => YoutubeSong.DownloadAudioAsync(videoInfo, downloadPath, Observer.Create<double>(progress => this.DownloadProgress = progress)));
        }

        private async Task DownloadFromYoutube(Func<Task> downloadFunction)
        {
            this.DownloadProgress = 0;
            this.DownloadFailed = false;

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
            await this.DownloadFromYoutube(() => YoutubeSong.DownloadVideoAsync(videoInfo, downloadPath, Observer.Create<double>(progress => this.DownloadProgress = progress)));
        }

        private async Task GetThumbnailAsync()
        {
            using (var webClient = new WebClient())
            {
                try
                {
                    byte[] imageBytes = await webClient.DownloadDataTaskAsync(((YoutubeSong)this.Model).ThumbnailSource);

                    if (imageBytes == null)
                    {
                        return;
                    }

                    using (var imageStream = new MemoryStream(imageBytes))
                    {
                        var image = new BitmapImage();

                        image.BeginInit();
                        image.StreamSource = imageStream;
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.EndInit();

                        image.Freeze();

                        this.Thumbnail = image;
                    }
                }

                catch (WebException)
                { } // We can't load the thumbnail, ignore it
            }
        }
    }
}