using Espera.Core;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Espera.View.ViewModels
{
    internal sealed class YoutubeSongViewModel : SongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<bool> hasThumbnail;
        private ImageSource thumbnail;

        public YoutubeSongViewModel(YoutubeSong wrapped)
            : base(wrapped)
        {
            this.OpenPathCommand = new ReactiveCommand();
            this.OpenPathCommand.Subscribe(x => Process.Start(this.Path));

            this.hasThumbnail = this.WhenAny(x => x.Thumbnail, x => x.Value)
                .Select(x => x != null)
                .ToProperty(this, x => x.HasThumbnail);
        }

        public string Description
        {
            get { return ((YoutubeSong)this.Model).Description; }
        }

        public bool HasThumbnail
        {
            get { return this.hasThumbnail.Value; }
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

        public int ViewCount
        {
            get { return ((YoutubeSong)this.Model).Views; }
        }

        public string Views
        {
            get { return String.Format("{0:N0}", ((YoutubeSong)this.Model).Views); }
        }

        private async void GetThumbnailAsync()
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