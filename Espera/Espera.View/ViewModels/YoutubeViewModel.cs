using System;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveUI;
using ReactiveUI.Legacy;
using Splat;

namespace Espera.View.ViewModels
{
    public sealed class YoutubeViewModel : NetworkSongViewModel<YoutubeSongViewModel, YoutubeSong>
    {
        private readonly ViewSettings viewSettings;
        private SortOrder ratingOrder;
        private SortOrder viewsOrder;
        private readonly ReactiveUI.Legacy.ReactiveCommand playNowCommand;

        public int RatingColumnWidth
        {
            get { return this.viewSettings.YoutubeRatingColumnWidth; }
            set { this.viewSettings.YoutubeRatingColumnWidth = value; }
        }

        public ReactiveCommand OrderByViewsCommand { get; private set; }

        public override ReactiveUI.Legacy.ReactiveCommand PlayNowCommand
        {
            get { return this.playNowCommand; }
        }

        public YoutubeViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, Guid accessToken, INetworkStatus networkstatus = null, IYoutubeSongFinder songFinder = null)
            : base(library, accessToken)
            : base(library, accessToken, coreSettings,
                song => new YoutubeSongViewModel(song, () => coreSettings.YoutubeDownloadPath),
                networkstatus,
                songFinder ?? new YoutubeSongFinder())
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            this.connectionError = new Subject<Unit>();
            this.playNowCommand = new ReactiveUI.Legacy.ReactiveCommand(this.Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin || !this.coreSettings.LockPlayPause));
            this.playNowCommand.RegisterAsyncTask(_ => this.Library.PlayInstantlyAsync(this.SelectedSongs.Select(vm => vm.Model), accessToken));
            this.OrderByRatingCommand = new ReactiveUI.Legacy.ReactiveCommand();
            this.OrderByRatingCommand.Subscribe(_ => this.ApplyOrder(SortHelpers.GetOrderByRating, ref this.ratingOrder));

            this.selectedSong = this.WhenAnyValue(x => x.SelectedSongs)
                .Select(x => x == null ? null : (YoutubeSongViewModel)this.SelectedSongs.FirstOrDefault())
                .ToProperty(this, x => x.SelectedSong);

            this.RefreshNetworkAvailabilityCommand = new ReactiveUI.Legacy.ReactiveCommand();

            var status = (networkstatus ?? new NetworkStatus());
            IObservable<bool> networkAvailable = this.RefreshNetworkAvailabilityCommand.ToUnit()
                .StartWith(Unit.Default)
                .Do(_ => this.Log().Info("Refreshing network availability"))
                .Select(_ => status.IsAvailable.Do(x => this.Log().Info("Network available: {0}", x))).Switch()
                .Replay(1).RefCount();

            this.isNetworkUnavailable = networkAvailable
                .Select(x => !x)
                .Merge(this.connectionError.Select(x => true))
                .ToProperty(this, x => x.IsNetworkUnavailable);

            // We need a default sorting order
            this.OrderByTitle();

            this.WhenAnyValue(x => x.SearchText).Skip(1).Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler).Select(_ => Unit.Default)
                .Merge(networkAvailable.Where(x => x).DistinctUntilChanged().ToUnit())
                .Select(_ => this.StartSearchAsync().ToObservable())
                // We don't use SelectMany, because we only care about the latest invocation and
                // don't want an old, still running request to override a request that is newer and faster
                .Switch()
                .Subscribe(x =>
                {
                    this.SelectableSongs = x;
                    this.SelectedSongs = this.SelectableSongs.Take(1).ToList();
                });
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

        public ReactiveUI.Legacy.ReactiveCommand OrderByRatingCommand { get; private set; }

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