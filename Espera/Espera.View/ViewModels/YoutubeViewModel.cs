using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    internal sealed class YoutubeViewModel : SongSourceViewModel<YoutubeSongViewModel>
    {
        private readonly CoreSettings coreSettings;
        private readonly ObservableAsPropertyHelper<bool> isNetworkUnavailable;
        private readonly IReactiveCommand playNowCommand;
        private readonly ObservableAsPropertyHelper<YoutubeSongViewModel> selectedSong;
        private readonly ViewSettings viewSettings;
        private SortOrder durationOrder;
        private bool isSearching;
        private SortOrder ratingOrder;
        private SortOrder titleOrder;
        private SortOrder viewsOrder;

        public YoutubeViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings)
            : base(library)
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            if (coreSettings == null)
                Throw.ArgumentNullException(() => coreSettings);

            this.viewSettings = viewSettings;
            this.coreSettings = coreSettings;

            this.playNowCommand = new ReactiveCommand();
            this.playNowCommand.RegisterAsyncTask(_ => this.Library.PlayInstantlyAsync(this.SelectedSongs.Select(vm => vm.Model)));

            this.selectedSong = this.WhenAnyValue(x => x.SelectedSongs)
                .Select(x => x == null ? null : this.SelectedSongs.FirstOrDefault())
                .Select(x => x == null ? null : (YoutubeSongViewModel)x)
                .ToProperty(this, x => x.SelectedSong);

            this.isNetworkUnavailable = Observable.FromEventPattern<NetworkAvailabilityChangedEventHandler, NetworkAvailabilityEventArgs>(
                h => NetworkChange.NetworkAvailabilityChanged += h,
                h => NetworkChange.NetworkAvailabilityChanged -= h)
                .Select(x => !x.EventArgs.IsAvailable)
                .Do(_ => this.StartSearch())
                .ToProperty(this, x => x.IsNetworkUnavailable, !NetworkInterface.GetIsNetworkAvailable());

            // We need a default sorting order
            this.OrderByTitle();

            // Create a default list
            this.StartSearch();
        }

        public int DurationColumnWidth
        {
            get { return this.viewSettings.YoutubeDurationColumnWidth; }
            set { this.viewSettings.YoutubeDurationColumnWidth = value; }
        }

        public bool IsNetworkUnavailable
        {
            get { return this.isNetworkUnavailable.Value; }
        }

        public bool IsSearching
        {
            get { return this.isSearching; }
            private set { this.RaiseAndSetIfChanged(ref this.isSearching, value); }
        }

        public int LinkColumnWidth
        {
            get { return this.viewSettings.YoutubeLinkColumnWidth; }
            set { this.viewSettings.YoutubeLinkColumnWidth = value; }
        }

        public override IReactiveCommand PlayNowCommand
        {
            get { return this.playNowCommand; }
        }

        public int RatingColumnWidth
        {
            get { return this.viewSettings.YoutubeRatingColumnWidth; }
            set { this.viewSettings.YoutubeRatingColumnWidth = value; }
        }

        public YoutubeSongViewModel SelectedSong
        {
            get { return this.selectedSong.Value; }
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
                .Select(song => new YoutubeSongViewModel(song, () => this.coreSettings.YoutubeDownloadPath))
                .SubscribeOn(RxApp.TaskpoolScheduler)
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