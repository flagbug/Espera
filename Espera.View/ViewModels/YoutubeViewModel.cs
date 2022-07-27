using System;
using Akavache;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveUI;
using Splat;

namespace Espera.View.ViewModels
{
    public class YoutubeViewModel : NetworkSongViewModel<YoutubeSongViewModel, YoutubeSong>
    {
        private readonly ViewSettings viewSettings;
        private SortOrder ratingOrder;
        private SortOrder viewsOrder;

        public YoutubeViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, Guid accessToken,
            IYoutubeSongFinder songFinder = null)
            : base(library, accessToken, coreSettings,
                song => new YoutubeSongViewModel(song, () => coreSettings.YoutubeDownloadPath),
                songFinder ??
                new YoutubeSongFinder(Locator.Current.GetService<IBlobCache>(BlobCacheKeys.RequestCacheContract)))
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            OrderByRatingCommand = ReactiveCommand.Create();
            OrderByRatingCommand.Subscribe(_ => ApplyOrder(SortHelpers.GetOrderByRating, ref ratingOrder));

            OrderByViewsCommand = ReactiveCommand.Create();
            OrderByViewsCommand.Subscribe(_ => ApplyOrder(SortHelpers.GetOrderByViews, ref viewsOrder));
        }

        public int DurationColumnWidth
        {
            get => viewSettings.YoutubeDurationColumnWidth;
            set => viewSettings.YoutubeDurationColumnWidth = value;
        }

        public int LinkColumnWidth
        {
            get => viewSettings.YoutubeLinkColumnWidth;
            set => viewSettings.YoutubeLinkColumnWidth = value;
        }

        public ReactiveCommand<object> OrderByRatingCommand { get; }

        public ReactiveCommand<object> OrderByViewsCommand { get; }

        public int RatingColumnWidth
        {
            get => viewSettings.YoutubeRatingColumnWidth;
            set => viewSettings.YoutubeRatingColumnWidth = value;
        }

        public int TitleColumnWidth
        {
            get => viewSettings.YoutubeTitleColumnWidth;
            set => viewSettings.YoutubeTitleColumnWidth = value;
        }

        public int ViewsColumnWidth
        {
            get => viewSettings.YoutubeViewsColumnWidth;
            set => viewSettings.YoutubeViewsColumnWidth = value;
        }
    }
}