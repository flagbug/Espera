using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;
using Splat;

namespace Espera.View.ViewModels
{
    /// <summary>
    /// The base class for songs that we get over the network (e.g YouTube and SoundCloud)
    /// </summary>
    public abstract class NetworkSongViewModel<TViewModel, TSong> : SongSourceViewModel<TViewModel>
        where TViewModel : ISongViewModelBase
        where TSong : Song
    {
        private readonly Func<TSong, TViewModel> modelToViewModelConverter;
        private readonly ReactiveCommand<Unit> playNowCommand;
        private readonly ObservableAsPropertyHelper<ISongViewModelBase> selectedSong;
        private readonly INetworkSongFinder<TSong> songFinder;
        private bool isSearching;
        private bool searchFailed;

        protected NetworkSongViewModel(Library library, Guid accessToken, CoreSettings coreSettings,
                Func<TSong, TViewModel> modelToViewModelConverter,
                INetworkSongFinder<TSong> songFinder = null)
            : base(library, coreSettings, accessToken)
        {
            if (modelToViewModelConverter == null)
                Throw.ArgumentNullException(() => modelToViewModelConverter);

            this.modelToViewModelConverter = modelToViewModelConverter;
            this.songFinder = songFinder;

            IObservable<bool> canPlayNow = this.Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin || !this.CoreSettings.LockPlayPause);
            this.playNowCommand = ReactiveCommand.CreateAsyncTask(canPlayNow,
                _ => this.Library.PlayInstantlyAsync(this.SelectedSongs.Select(vm => vm.Model), accessToken));

            this.selectedSong = this.WhenAnyValue(x => x.SelectedSongs)
                .Select(x => x == null ? null : this.SelectedSongs.FirstOrDefault())
                .ToProperty(this, x => x.SelectedSong);

            this.Search = ReactiveCommand.Create();

            this.WhenAnyValue(x => x.SearchText, x => x.Trim()).DistinctUntilChanged().Skip(1)
                .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler).Select(_ => Unit.Default)
                .Merge(this.Search.ToUnit())
                .StartWith(Unit.Default)
                .Select(_ => this.StartSearchAsync())
                .Switch()
                .Select(x => x.OrderBy(this.SongOrderFunc).ToList())
                .Subscribe(x =>
                {
                    this.SelectableSongs = x;
                    this.SelectedSongs = (IEnumerable<ISongViewModelBase>)this.SelectableSongs.Take(1).ToList();
                });
        }

        public override DefaultPlaybackAction DefaultPlaybackAction
        {
            get { return DefaultPlaybackAction.AddToPlaylist; }
        }

        public bool IsSearching
        {
            get { return this.isSearching; }
            private set { this.RaiseAndSetIfChanged(ref this.isSearching, value); }
        }

        public override ReactiveCommand<Unit> PlayNowCommand
        {
            get { return this.playNowCommand; }
        }

        /// <summary>
        /// Performs a manual search, instead of an automatic search when the search text has changed.
        /// </summary>
        public ReactiveCommand<object> Search { get; private set; }

        public bool SearchFailed
        {
            get { return this.searchFailed; }
            private set { this.RaiseAndSetIfChanged(ref this.searchFailed, value); }
        }

        public ISongViewModelBase SelectedSong
        {
            get { return this.selectedSong.Value; }
        }

        private IObservable<IReadOnlyList<TViewModel>> StartSearchAsync()
        {
            return Observable.Defer(() =>
            {
                this.IsSearching = true;
                this.SelectedSongs = null;
                this.SearchFailed = false;

                return this.songFinder.GetSongsAsync(this.SearchText);
            })
            .Catch<IReadOnlyList<TSong>, NetworkSongFinderException>(ex =>
            {
                this.Log().ErrorException("Failed to load songs from the network", ex);
                this.SearchFailed = true;
                return Observable.Return(new List<TSong>());
            })
            .Select(x => x.Select(y => this.modelToViewModelConverter(y)).ToList())
            .Finally(() => this.IsSearching = false);
        }
    }
}