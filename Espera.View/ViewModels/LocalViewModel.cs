using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Binding;
using DynamicData.Controllers;
using DynamicData.Operators;
using DynamicData.ReactiveUI;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public class LocalViewModel : SongSourceViewModel<LocalSongViewModel>
    {
        private readonly ArtistViewModel allArtistsViewModel;
        private readonly SortOrder artistOrder;
        private readonly ReactiveList<ArtistViewModel> artists;
        private readonly Subject<Unit> artistUpdateSignal;
        private readonly ObservableAsPropertyHelper<bool> isUpdating;
        private readonly ReactiveCommand<Unit> playNowCommand;
        private readonly ObservableAsPropertyHelper<bool> showAddSongsHelperMessage;
        private readonly ReactiveList<LocalSongViewModel> songs;
        private readonly ViewSettings viewSettings;
        private ArtistViewModel selectedArtist;

        public LocalViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, Guid accessToken)
            : base(library, coreSettings, accessToken)
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            this.artistUpdateSignal = new Subject<Unit>();

            this.allArtistsViewModel = new ArtistViewModel("All Artists");

            // We need a default sorting order
            this.ApplyOrder(SortHelpers.GetOrderByArtist<LocalSongViewModel>, ref this.artistOrder);

            this.SelectedArtist = this.allArtistsViewModel;

            this.songs = new ReactiveList<LocalSongViewModel>();
            this.SelectableSongs = this.songs;
            this.artists = new ReactiveList<ArtistViewModel>();
            var songSource = new SourceCache<LocalSong, Guid>(x => x.Guid);

            IObservableCache<LocalSongViewModel, Guid> songsCache = songSource.Connect()
                .Transform(x => new LocalSongViewModel(x))
                .DisposeMany()
                .AsObservableCache();

            IObservableCache<ArtistViewModel, ArtistViewModel.ArtistString> artistsCache = songSource.Connect()
                .Group(x => (ArtistViewModel.ArtistString)x.Artist)
                .Transform(x => new ArtistViewModel(x.Key, x.Cache.Connect().WhereReasonsAre(ChangeReason.Add).Flatten().Select(y => y.Current.ArtworkKey)))
                .DisposeMany()
                .AsObservableCache();

            IObservable<Func<LocalSongViewModel, bool>> searchEngine = this.WhenAnyValue(x => x.SearchText)
                .Select(searchText => new SearchEngine(searchText))
                .Select(engine => new Func<LocalSongViewModel, bool>(song => engine.Filter(song.Model)));

            IObservable<Func<LocalSongViewModel, bool>> artistFilter = this.WhenAnyValue(x => x.SelectedArtist)
                .Select(artist => new Func<LocalSongViewModel, bool>(song => artist.IsAllArtists || song.Artist.Equals(artist.Name, StringComparison.InvariantCultureIgnoreCase)));

            var filteredSource = songsCache.Connect()
                .Filter(searchEngine)
                .Publish()
                .RefCount();

            filteredSource
                .Filter(artistFilter)
                .Sort(SortExpressionComparer<LocalSongViewModel>.Ascending(x => SortHelpers.RemoveArtistPrefixes(x.Artist)).ThenByAscending(x => x.Album).ThenByAscending(x => x.TrackNumber))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(this.songs)
                .DisposeMany()
                .Subscribe();

            var filteredArtistGrouping = filteredSource
                .Group(x => x.Artist)
                .Convert(x => x.Key)
                .ToCollection()
                .Select(x => new HashSet<string>(x, StringComparer.InvariantCultureIgnoreCase))
                .Select(artists => new Func<ArtistViewModel, bool>(artistViewModel => artists.Contains(artistViewModel.Name)));

            artistsCache.Connect()
                .Filter(filteredArtistGrouping)
                .StartWithItem(this.allArtistsViewModel, Guid.NewGuid().ToString())
                .Sort(new ArtistViewModel.Comparer(), SortOptimisations.ComparesImmutableValuesOnly)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(this.artists)
                .DisposeMany()
                .Subscribe();

            this.Library.SongsUpdated
                .Buffer(TimeSpan.FromSeconds(1), RxApp.TaskpoolScheduler)
                .Where(x => x.Any())
                .ToUnit()
                .StartWith(Unit.Default)
                .Select(_ => this.Library.Songs)
                .Subscribe(x => songSource.Edit(update =>
                {
                    update.Clear();
                    update.AddOrUpdate(x);
                }));

            this.playNowCommand = ReactiveCommand.CreateAsyncTask(this.Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin || !coreSettings.LockPlayPause), _ =>
                {
                    int songIndex = this.SelectableSongs.TakeWhile(x => x.Model != this.SelectedSongs.First().Model).Count();

                    return this.Library.PlayInstantlyAsync(this.SelectableSongs.Skip(songIndex).Select(x => x.Model), accessToken);
                });

            this.showAddSongsHelperMessage = this.Library.SongsUpdated
                .StartWith(Unit.Default)
                .Select(_ => this.Library.Songs.Count == 0)
                .TakeWhile(x => x)
                .Concat(Observable.Return(false))
                .ToProperty(this, x => x.ShowAddSongsHelperMessage);

            this.isUpdating = this.Library.WhenAnyValue(x => x.IsUpdating)
                .ToProperty(this, x => x.IsUpdating);

            this.OpenTagEditor = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedSongs, x => x.Any()));
        }

        public IReadOnlyReactiveList<ArtistViewModel> Artists => this.artists;

        public override DefaultPlaybackAction DefaultPlaybackAction
        {
            get { return this.CoreSettings.DefaultPlaybackAction; }
        }

        public int DurationColumnWidth
        {
            get { return this.viewSettings.LocalDurationColumnWidth; }
            set { this.viewSettings.LocalDurationColumnWidth = value; }
        }

        public int GenreColumnWidth
        {
            get { return this.viewSettings.LocalGenreColumnWidth; }
            set { this.viewSettings.LocalGenreColumnWidth = value; }
        }

        public bool IsUpdating
        {
            get { return this.isUpdating.Value; }
        }

        public ReactiveCommand<object> OpenTagEditor { get; private set; }

        public override ReactiveCommand<Unit> PlayNowCommand
        {
            get { return this.playNowCommand; }
        }

        public ArtistViewModel SelectedArtist
        {
            get { return this.selectedArtist; }

            set
            {
                // We don't ever want the selected artist to be null
                this.RaiseAndSetIfChanged(ref this.selectedArtist, value ?? this.allArtistsViewModel);
            }
        }

        public bool ShowAddSongsHelperMessage
        {
            get { return this.showAddSongsHelperMessage.Value; }
        }

        public int TitleColumnWidth
        {
            get { return this.viewSettings.LocalTitleColumnWidth; }
            set { this.viewSettings.LocalTitleColumnWidth = value; }
        }
    }
}