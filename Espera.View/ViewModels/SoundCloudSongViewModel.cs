using System;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Akavache;
using Espera.Core;
using ReactiveUI;
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
            hasThumbnail = this.WhenAnyValue(x => x.Thumbnail)
                .Select(x => x != null)
                .ToProperty(this, x => x.HasThumbnail);
        }

        public string Description => ((SoundCloudSong)Model).Description;

        public bool HasThumbnail => hasThumbnail.Value;

        public bool IsLoadingThumbnail
        {
            get => isLoadingThumbnail;
            private set => this.RaiseAndSetIfChanged(ref isLoadingThumbnail, value);
        }

        public int PlaybackCount => ((SoundCloudSong)Model).PlaybackCount.GetValueOrDefault();

        public string Playbacks => string.Format(NumberFormatInfo.InvariantInfo, "{0:N0}", PlaybackCount);

        public ImageSource Thumbnail
        {
            get
            {
                if (thumbnail == null) GetThumbnailAsync();

                return thumbnail;
            }

            private set => this.RaiseAndSetIfChanged(ref thumbnail, value);
        }

        public string Uploader => ((SoundCloudSong)Model).User.Username;

        private async Task GetThumbnailAsync()
        {
            Uri artworkUrl = ((SoundCloudSong)Model).ArtworkUrl;

            if (artworkUrl == null)
                return;

            // Get a non-shitty resolution <c>
            // https: //developers.soundcloud.com/docs/api/reference#tracks <c/>
            artworkUrl = new Uri(artworkUrl.ToString().Replace("large", "t300x300"));

            IsLoadingThumbnail = true;

            try
            {
                IBitmap image = await BlobCache.InMemory.LoadImageFromUrl(artworkUrl.ToString(),
                    absoluteExpiration: DateTimeOffset.Now + TimeSpan.FromMinutes(60));

                Thumbnail = image.ToNative();
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Failed to download SoundCloud artwork", ex);
            }

            IsLoadingThumbnail = false;
        }
    }
}