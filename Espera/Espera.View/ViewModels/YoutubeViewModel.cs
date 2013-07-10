using System.Diagnostics;
using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    internal sealed class YoutubeViewModel : SongSourceViewModel<YoutubeSongViewModel>
    {
        private readonly IReactiveCommand playNowCommand;
        private readonly ObservableAsPropertyHelper<YoutubeSongViewModel> selectedSong;
        private SortOrder durationOrder;
        private bool isSearching;
        private SortOrder ratingOrder;
        private SortOrder titleOrder;
        private SortOrder viewsOrder;

        public YoutubeViewModel(Library library)
            : base(library)
        {
            this.playNowCommand = new ReactiveCommand();
            this.playNowCommand.Subscribe(x => this.Library.PlayInstantlyAsync(this.SelectedSongs.Select(vm => vm.Model)));

            this.selectedSong = this.WhenAny(x => x.SelectedSongs, x => x.Value)
                .Select(x => x == null ? null : this.SelectedSongs.FirstOrDefault())
                .Select(x => x == null ? null : (YoutubeSongViewModel)x)
                .ToProperty(this, x => x.SelectedSong);

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
            private set { this.RaiseAndSetIfChanged(ref this.isSearching, value); }
        }

        public int LinkColumnWidth
        {
            get { return Settings.Default.YoutubeLinkColumnWidth; }
            set { Settings.Default.YoutubeLinkColumnWidth = value; }
        }

        public override IReactiveCommand PlayNowCommand
        {
            get { return this.playNowCommand; }
        }

        public int RatingColumnWidth
        {
            get { return Settings.Default.YoutubeRatingColumnWidth; }
            set { Settings.Default.YoutubeRatingColumnWidth = value; }
        }

        public YoutubeSongViewModel SelectedSong
        {
            get { return this.selectedSong.Value; }
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

            this.UpdateSelectableSongs();
        }

        private void UpdateSelectableSongs()
        {
            var finder = new YoutubeSongFinder(this.SearchText);

            var songs = new List<YoutubeSongViewModel>();

            finder.GetSongs()
                .Select(song => new YoutubeSongViewModel(song))
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(song => songs.Add(song), () =>
                {
                    this.IsSearching = false;

                    this.SelectableSongs = songs
                        .OrderBy(this.SongOrderFunc)
                        .ToList();

                    this.SelectedSongs = this.SelectableSongs.Take(1).ToList();
                });
        }
    }
}