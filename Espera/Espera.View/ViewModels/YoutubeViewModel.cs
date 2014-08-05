using System;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    public sealed class YoutubeViewModel : NetworkSongViewModel<YoutubeSongViewModel, YoutubeSong>
    {
        private readonly ViewSettings viewSettings;
        private SortOrder durationOrder;
        private SortOrder ratingOrder;
        private SortOrder viewsOrder;

        public YoutubeViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, Guid accessToken, INetworkStatus networkstatus = null, IYoutubeSongFinder songFinder = null)
            : base(library, accessToken, coreSettings,
                song => new YoutubeSongViewModel(song, () => coreSettings.YoutubeDownloadPath),
                networkstatus,
                songFinder ?? new YoutubeSongFinder())
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;
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

        public void OrderByDuration()
        {
            this.ApplyOrder(SortHelpers.GetOrderByDuration<YoutubeSongViewModel>, ref this.durationOrder);
        }

        public void OrderByRating()
        {
            this.ApplyOrder(SortHelpers.GetOrderByRating, ref this.ratingOrder);
        }

        public void OrderByViews()
        {
            this.ApplyOrder(SortHelpers.GetOrderByViews, ref this.viewsOrder);
        }
    }
}