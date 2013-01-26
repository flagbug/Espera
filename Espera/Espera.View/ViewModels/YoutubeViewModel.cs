using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Espera.View.ViewModels
{
    internal sealed class YoutubeViewModel : SongSourceViewModel<YoutubeSongViewModel>
    {
        private List<YoutubeSong> currentSongs;
        private SortOrder durationOrder;
        private bool isSearching;
        private SortOrder ratingOrder;
        private SortOrder titleOrder;
        private SortOrder viewsOrder;

        public YoutubeViewModel(Library library)
            : base(library)
        {
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
            private set { this.RaiseAndSetIfChanged(value); }
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
            this.currentSongs = new List<YoutubeSong>();

            if (this.IsSearching || this.currentSongs == null)
            {
                var finder = new YoutubeSongFinder(this.SearchText);

                finder.SongFound.Subscribe(currentSongs.Add, ex => { }); //TODO: Handle error

                finder.Execute();

                this.IsSearching = false;
            }

            this.SelectableSongs = this.currentSongs
                .Select(song => new YoutubeSongViewModel(song))
                .OrderBy(this.SongOrderFunc)
                .ToList();

            this.SelectedSongs = this.SelectableSongs.Take(1).ToList();
        }
    }
}