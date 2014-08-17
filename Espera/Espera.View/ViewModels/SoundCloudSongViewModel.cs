using System.Globalization;
using Espera.Core;
using ReactiveUI;
using System;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Splat;

namespace Espera.View.ViewModels
{
    internal class SoundCloudSongViewModel : SongViewModelBase, IEnableLogger
    {
        private readonly ObservableAsPropertyHelper<bool> hasThumbnail;
        private bool isLoadingThumbnail;
        private ImageSource thumbnail;

        public SoundCloudSongViewModel(SoundCloudSong model)
            : base(model)
        {
            this.hasThumbnail = this.WhenAnyValue(x => x.Thumbnail)
                .Select(x => x != null)
                .ToProperty(this, x => x.HasThumbnail);
        }

        public string Description
        {
            get { return ((SoundCloudSong)this.Model).Description; }
        }

        public bool HasThumbnail
        {
            get { return this.hasThumbnail.Value; }
        }

        public bool IsLoadingThumbnail
        {
            get { return this.isLoadingThumbnail; }
            private set { this.RaiseAndSetIfChanged(ref this.isLoadingThumbnail, value); }
        }

        public int PlaybackCount
        {
            get { return ((SoundCloudSong)this.Model).PlaybackCount.GetValueOrDefault(); }
        }

        public string Playbacks
        {
            get { return String.Format(NumberFormatInfo.InvariantInfo, "{0:N0}", this.PlaybackCount); }
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

        public string Uploader
        {
            get { return ((SoundCloudSong)this.Model).User.Username; }
        }

        private async Task GetThumbnailAsync()
        {
            this.IsLoadingThumbnail = true;

            using (var client = new HttpClient())
            {
                try
                {
                    Uri artworkUrl = ((SoundCloudSong)this.Model).ArtworkUrl;

                    if (artworkUrl == null)
                        return;

                    byte[] imageBytes = await client.GetByteArrayAsync(artworkUrl);

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

                catch (HttpRequestException ex)
                {
                    this.Log().ErrorException("Failed to download SoundCloud artwork", ex);
                }

                finally
                {
                    this.IsLoadingThumbnail = false;
                }
            }
        }
    }
}