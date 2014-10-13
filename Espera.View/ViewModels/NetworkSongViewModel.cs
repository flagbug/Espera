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
        private readonly Subject<Unit> connectionError;
        private readonly CoreSettings coreSettings;
        private readonly ObservableAsPropertyHelper<bool> isNetworkUnavailable;
        private readonly Func<TSong, TViewModel> modelToViewModelConverter;
        private readonly ReactiveCommand<Unit> playNowCommand;
        private readonly ObservableAsPropertyHelper<ISongViewModelBase> selectedSong;
        private readonly INetworkSongFinder<TSong> songFinder;
        private bool isSearching;

        protected NetworkSongViewModel(Library library, Guid accessToken, CoreSettings coreSettings,
                Func<TSong, TViewModel> modelToViewModelConverter, INetworkStatus networkstatus = null,
                INetworkSongFinder<TSong> songFinder = null)
            : base(library, accessToken)
        {
            if (coreSettings == null)
                Throw.ArgumentNullException(() => coreSettings);

            if (modelToViewModelConverter == null)
                Throw.ArgumentNullException(() => modelToViewModelConverter);

            this.coreSettings = coreSettings;
            this.modelToViewModelConverter = modelToViewModelConverter;
            this.songFinder = songFinder;

            this.connectionError = new Subject<Unit>();

            IObservable<bool> canPlayNow = this.Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin || !this.coreSettings.LockPlayPause);
            this.playNowCommand = ReactiveCommand.CreateAsyncTask(canPlayNow,
                _ => this.Library.PlayInstantlyAsync(this.SelectedSongs.Select(vm => vm.Model), accessToken));

            this.selectedSong = this.WhenAnyValue(x => x.SelectedSongs)
                .Select(x => x == null ? null : this.SelectedSongs.FirstOrDefault())
                .ToProperty(this, x => x.SelectedSong);

            this.RefreshNetworkAvailabilityCommand = ReactiveCommand.Create();

            var status = (networkstatus ?? new NetworkStatus());
            IObservable<bool> networkAvailable = this.RefreshNetworkAvailabilityCommand.ToUnit()
                .StartWith(Unit.Default)
                .Do(_ => this.Log().Info("Refreshing network availability"))
                .Select(_ => status.GetIsAvailableAsync().Do(x => this.Log().Info("Network available: {0}", x)))
                .Switch()
                .Replay(1)
                .RefCount();

            this.isNetworkUnavailable = networkAvailable
                .Select(x => !x)
                .Merge(this.connectionError.Select(x => true))
                .ToProperty(this, x => x.IsNetworkUnavailable);

            this.WhenAnyValue(x => x.SearchText, x => x.Trim()).DistinctUntilChanged().Skip(1)
                .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler).Select(_ => Unit.Default)
                .Merge(networkAvailable.Where(x => x).DistinctUntilChanged().ToUnit())
                .Select(_ => this.StartSearchAsync())
                // We don't use SelectMany, because we only care about the latest invocation and
                // don't want an old, still running request to override a request that is newer and faster
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

        public bool IsNetworkUnavailable
        {
            get { return this.isNetworkUnavailable.Value; }
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

        public ReactiveCommand<object> RefreshNetworkAvailabilityCommand { get; private set; }

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

                return this.songFinder.GetSongsAsync(this.SearchText);
            })
            .Select(x => x.Select(y => this.modelToViewModelConverter(y)).ToList())
            .Catch<IReadOnlyList<TViewModel>, NetworkSongFinderException>(ex =>
            {
                this.Log().ErrorException("Failed to load songs from the network", ex);
                this.connectionError.OnNext(Unit.Default);
                return Observable.Return(new List<TViewModel>());
            })
            .Finally(() => this.IsSearching = false);
        }
    }
}