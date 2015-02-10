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

        public YoutubeViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, Guid accessToken, IYoutubeSongFinder songFinder = null)
            : base(library, accessToken, coreSettings,
                song => new YoutubeSongViewModel(song, () => coreSettings.YoutubeDownloadPath),
                songFinder ?? new YoutubeSongFinder(Locator.Current.GetService<IBlobCache>(BlobCacheKeys.RequestCacheContract)))
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            this.OrderByRatingCommand = ReactiveCommand.Create();
            this.OrderByRatingCommand.Subscribe(_ => this.ApplyOrder(SortHelpers.GetOrderByRating, ref this.ratingOrder));

            this.OrderByViewsCommand = ReactiveCommand.Create();
            this.OrderByViewsCommand.Subscribe(_ => this.ApplyOrder(SortHelpers.GetOrderByViews, ref this.viewsOrder));
        }

        public int DurationColumnWidth
        {
            get { return this.viewSettings.YoutubeDurationColumnWidth; }
            set { this.viewSettings.YoutubeDurationColumnWidth = value; }
        }

        public int LinkColumnWidth
        {
            get { return this.viewSettings.YoutubeLinkColumnWidth; }
            set { this.viewSettings.YoutubeLinkColumnWidth = value; }
        }

        public ReactiveCommand<object> OrderByRatingCommand { get; private set; }

        public ReactiveCommand<object> OrderByViewsCommand { get; private set; }

        public int RatingColumnWidth
        {
            get { return this.viewSettings.YoutubeRatingColumnWidth; }
            set { this.viewSettings.YoutubeRatingColumnWidth = value; }
        }

        public int TitleColumnWidth
        {
            get { return this.viewSettings.YoutubeTitleColumnWidth; }
            set { this.viewSettings.YoutubeTitleColumnWidth = value; }
        }

        public int ViewsColumnWidth
        {
            get { return this.viewSettings.YoutubeViewsColumnWidth; }
            set { this.viewSettings.YoutubeViewsColumnWidth = value; }
        }
    }
}