using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Espera.View.ViewModels
{
    internal sealed class YoutubeViewModel : SongSourceViewModel<YoutubeSongViewModel>
    {
        private IEnumerable<YoutubeSong> currentSongs;
        private SortOrder durationOrder;
        private bool isSearching;
        private SortOrder ratingOrder;
        private string searchText;
        private SortOrder titleOrder;
        private SortOrder viewsOrder;

        public YoutubeViewModel(Library library)
            : base(library)
        {
            this.searchText = String.Empty;

            // We need a default sorting order
            this.OrderByTitle();

            // Create a default list
            this.StartSearch();
        }

        public int DurationColumnWidth
        {
            get { return Settings.Default.YoutubeDurationColumnWidth; }
            set { Settings.Default.YoutubeDurationColumnWidth = value; }
        }

        public bool IsSearching
        {
            get { return this.isSearching; }
            private set
            {
                if (this.IsSearching != value)
                {
                    this.isSearching = value;
                    this.NotifyOfPropertyChange(() => this.IsSearching);
                }
            }
        }

        public int LinkColumnWidth
        {
            get { return Settings.Default.YoutubeLinkColumnWidth; }
            set { Settings.Default.YoutubeLinkColumnWidth = value; }
        }

        public int RatingColumnWidth
        {
            get { return Settings.Default.YoutubeRatingColumnWidth; }
            set { Settings.Default.YoutubeRatingColumnWidth = value; }
        }

        public override string SearchText
        {
            get { return this.searchText; }
            set
            {
                if (this.SearchText != value)
                {
                    this.searchText = value;
                    this.NotifyOfPropertyChange(() => this.SearchText);
                }
            }
        }

        public int TitleColumnWidth
        {
            get { return Settings.Default.YoutubeTitleColumnWidth; }
            set { Settings.Default.YoutubeTitleColumnWidth = value; }
        }

        public int ViewsColumnWidth
        {
            get { return Settings.Default.YoutubeViewsColumnWidth; }
            set { Settings.Default.YoutubeViewsColumnWidth = value; }
        }

        public void OrderByDuration()
        {
            this.ApplyOrder(SortHelpers.GetOrderByDuration<YoutubeSongViewModel>, ref this.durationOrder);
        }

        public void OrderByRating()
        {
            this.ApplyOrder(SortHelpers.GetOrderByRating, ref this.ratingOrder);
        }

        public void OrderByTitle()
        {
            this.ApplyOrder(SortHelpers.GetOrderByTitle<YoutubeSongViewModel>, ref this.titleOrder);
        }

        public void OrderByViews()
        {
            this.ApplyOrder(SortHelpers.GetOrderByViews, ref this.viewsOrder);
        }

        public void StartSearch()
        {
            this.IsSearching = true;

            Task.Factory.StartNew(this.UpdateSelectableSongs);
        }

        private void UpdateSelectableSongs()
        {
            if (this.IsSearching || this.currentSongs == null)
            {
                var finder = new YoutubeSongFinder(this.SearchText);
                finder.Execute();

                this.IsSearching = false;

                this.currentSongs = finder.SongFound.ToEnumerable();
            }

            this.SelectableSongs = currentSongs
                .Select(song => new YoutubeSongViewModel(song))
                .OrderBy(this.SongOrderFunc)
                .ToList();

            this.SelectedSongs = this.SelectableSongs.Take(1).ToList();
        }
    }
}