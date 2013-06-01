using Espera.Core;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Espera.View.ViewModels
{
    internal sealed class YoutubeSongViewModel : SongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<bool> hasThumbnail;
        private BitmapImage thumbnail;

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
            BitmapImage image = await Task.Run(() =>
            {
                using (var webClient = new WebClient())
                {
                    try
                    {
                        byte[] imageBytes = webClient.DownloadData(((YoutubeSong)this.Model).ThumbnailSource);

                        if (imageBytes == null)
                        {
                            return null;
                        }

                        using (var imageStream = new MemoryStream(imageBytes))
                        {
                            var img = new BitmapImage();

                            img.BeginInit();
                            img.StreamSource = imageStream;
                            img.CacheOption = BitmapCacheOption.OnLoad;
                            img.EndInit();

                            img.Freeze();

                            return img;
                        }
                    }

                    catch (WebException)
                    {
                        return null;
                    }
                }
            });

            if (image != null)
            {
                this.thumbnail = image;
                this.RaisePropertyChanged("Thumbnail");
            }
        }
    }
}