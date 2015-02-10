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
using Akavache;
using Splat;

namespace Espera.View.ViewModels
{
    public class SoundCloudSongViewModel : SongViewModelBase
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
            Uri artworkUrl = ((SoundCloudSong)this.Model).ArtworkUrl;

            if (artworkUrl == null)
                return;

            // Get a non-shitty resolution <c>
            // https: //developers.soundcloud.com/docs/api/reference#tracks <c/>
            artworkUrl = new Uri(artworkUrl.ToString().Replace("large", "t300x300"));

            this.IsLoadingThumbnail = true;

            try
            {
                IBitmap image = await BlobCache.InMemory.LoadImageFromUrl(artworkUrl.ToString(), absoluteExpiration: DateTimeOffset.Now + TimeSpan.FromMinutes(60));

                this.Thumbnail = image.ToNative();
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to download SoundCloud artwork", ex);
            }

            this.IsLoadingThumbnail = false;
        }
    }
}