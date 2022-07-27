using System;
using System.Collections.Generic;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    /// <summary>
    ///     The base class for songs that we get over the network (e.g YouTube and SoundCloud)
    /// </summary>
    public abstract class NetworkSongViewModel<TViewModel, TSong> : SongSourceViewModel<TViewModel>
        where TViewModel : ISongViewModelBase
        where TSong : Song
    {
        private readonly Func<TSong, TViewModel> modelToViewModelConverter;
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

            var canPlayNow = Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin || !CoreSettings.LockPlayPause);
            PlayNowCommand = ReactiveCommand.CreateAsyncTask(canPlayNow,
                _ => Library.PlayInstantlyAsync(SelectedSongs.Select(vm => vm.Model), accessToken));

            selectedSong = this.WhenAnyValue(x => x.SelectedSongs)
                .Select(x => x == null ? null : SelectedSongs.FirstOrDefault())
                .ToProperty(this, x => x.SelectedSong);

            Search = ReactiveCommand.Create();

            this.WhenAnyValue(x => x.SearchText, x => x.Trim()).DistinctUntilChanged().Skip(1)
                .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler).Select(_ => Unit.Default)
                .Merge(Search.ToUnit())
                .StartWith(Unit.Default)
                .Select(_ => StartSearchAsync())
                .Switch()
                .Select(x => x.OrderBy(SongOrderFunc).ToList())
                .Subscribe(x =>
                {
                    SelectableSongs = x;
                    SelectedSongs = (IEnumerable<ISongViewModelBase>)SelectableSongs.Take(1).ToList();
                });
        }

        public override DefaultPlaybackAction DefaultPlaybackAction => DefaultPlaybackAction.AddToPlaylist;

        public bool IsSearching
        {
            get => isSearching;
            private set => this.RaiseAndSetIfChanged(ref isSearching, value);
        }

        public override ReactiveCommand<Unit> PlayNowCommand { get; }

        /// <summary>
        ///     Performs a manual search, instead of an automatic search when the search text has changed.
        /// </summary>
        public ReactiveCommand<object> Search { get; }

        public bool SearchFailed
        {
            get => searchFailed;
            private set => this.RaiseAndSetIfChanged(ref searchFailed, value);
        }

        public ISongViewModelBase SelectedSong => selectedSong.Value;

        private IObservable<IReadOnlyList<TViewModel>> StartSearchAsync()
        {
            return Observable.Defer(() =>
                {
                    IsSearching = true;
                    SelectedSongs = null;
                    SearchFailed = false;

                    return songFinder.GetSongsAsync(SearchText);
                })
                .Catch<IReadOnlyList<TSong>, NetworkSongFinderException>(ex =>
                {
                    this.Log().ErrorException("Failed to load songs from the network", ex);
                    SearchFailed = true;
                    return Observable.Return(new List<TSong>());
                })
                .Select(x => x.Select(y => modelToViewModelConverter(y)).ToList())
                .Finally(() => IsSearching = false);
        }
    }
}