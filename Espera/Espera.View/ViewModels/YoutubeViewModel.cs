using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public void OrderByDuration()
        {
            this.SongOrderFunc = SortHelpers.GetOrderByDuration<YoutubeSongViewModel>(this.durationOrder);
            SortHelpers.InverseOrder(ref this.durationOrder);

            this.ApplyOrder();
        }

        public void OrderByRating()
        {
            this.SongOrderFunc = SortHelpers.GetOrderByRating(this.ratingOrder);
            SortHelpers.InverseOrder(ref this.ratingOrder);

            this.ApplyOrder();
        }

        public void OrderByTitle()
        {
            this.SongOrderFunc = SortHelpers.GetOrderByTitle<YoutubeSongViewModel>(this.titleOrder);
            SortHelpers.InverseOrder(ref this.titleOrder);

            this.ApplyOrder();
        }

        public void StartSearch()
        {
            this.IsSearching = true;

            Task.Factory.StartNew(this.UpdateSelectableSongs);
        }

        protected override void UpdateSelectableSongs()
        {
            if (this.IsSearching || this.currentSongs == null)
            {
                var finder = new YoutubeSongFinder(this.SearchText);
                finder.Start();

                this.IsSearching = false;

                this.currentSongs = finder.SongsFound;
            }

            this.SelectableSongs = currentSongs
                .Select(song => new YoutubeSongViewModel(song))
                .OrderBy(this.SongOrderFunc)
                .ToList();
        }
    }
}