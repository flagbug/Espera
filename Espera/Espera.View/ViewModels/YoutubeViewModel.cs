using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public sealed class YoutubeViewModel : SongSourceViewModel<YoutubeSongViewModel>
    {
        private readonly Subject<Unit> connectionError;
        private readonly CoreSettings coreSettings;
        private readonly ObservableAsPropertyHelper<bool> isNetworkUnavailable;
        private readonly IReactiveCommand playNowCommand;
        private readonly ObservableAsPropertyHelper<YoutubeSongViewModel> selectedSong;
        private readonly IYoutubeSongFinder songFinder;
        private readonly ViewSettings viewSettings;
        private SortOrder durationOrder;
        private bool isSearching;
        private SortOrder ratingOrder;
        private SortOrder titleOrder;
        private SortOrder viewsOrder;

        public YoutubeViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, Guid accessToken, INetworkStatus networkstatus = null, IYoutubeSongFinder songFinder = null)
            : base(library, accessToken)
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            if (coreSettings == null)
                Throw.ArgumentNullException(() => coreSettings);

            this.viewSettings = viewSettings;
            this.coreSettings = coreSettings;
            this.songFinder = songFinder ?? new YoutubeSongFinder();

            this.connectionError = new Subject<Unit>();
            this.playNowCommand = this.Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin || !this.coreSettings.LockPlayPause)
                .ToCommand();
            this.playNowCommand.RegisterAsyncTask(_ => this.Library.PlayInstantlyAsync(this.SelectedSongs.Select(vm => vm.Model), accessToken));

            this.selectedSong = this.WhenAnyValue(x => x.SelectedSongs)
                .Select(x => x == null ? null : (YoutubeSongViewModel)this.SelectedSongs.FirstOrDefault())
                .ToProperty(this, x => x.SelectedSong);

            var status = networkstatus ?? new NetworkStatus();
            this.isNetworkUnavailable = status.IsAvailable
                .Select(x => !x)
                .Merge(this.connectionError.Select(x => true))
                .ToProperty(this, x => x.IsNetworkUnavailable);

            // We need a default sorting order
            this.OrderByTitle();

            this.WhenAnyValue(x => x.SearchText).Skip(1).Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler).Select(_ => Unit.Default)
                .Merge(status.IsAvailable.Where(x => x).Select(_ => Unit.Default))
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

        private async Task<IReadOnlyList<YoutubeSongViewModel>> StartSearchAsync()
        {
            this.IsSearching = true;
            this.SelectedSongs = null;

            try
            {
                IReadOnlyList<YoutubeSong> songs = await this.songFinder.GetSongsAsync(this.SearchText);

                return songs.Select(x => new YoutubeSongViewModel(x, () => this.coreSettings.YoutubeDownloadPath)).ToList();
            }

            catch (Exception)
            {
                this.connectionError.OnNext(Unit.Default);

                return new List<YoutubeSongViewModel>();
            }

            finally
            {
                this.IsSearching = false;
            }
        }
    }
}