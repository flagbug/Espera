using System;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    public class LocalViewModel : SongSourceViewModel<LocalSongViewModel>
    {
        private readonly ReactiveList<ArtistViewModel> allArtists;
        private readonly ArtistViewModel allArtistsViewModel;
        private readonly SortOrder artistOrder;
        private readonly Subject<Unit> artistUpdateSignal;
        private readonly ObservableAsPropertyHelper<bool> isUpdating;
        private readonly ObservableAsPropertyHelper<bool> showAddSongsHelperMessage;
        private readonly ViewSettings viewSettings;
        private ILookup<string, Song> filteredSongs;
        private ArtistViewModel selectedArtist;

        public LocalViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, Guid accessToken)
            : base(library, coreSettings, accessToken)
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            artistUpdateSignal = new Subject<Unit>();

            allArtistsViewModel = new ArtistViewModel("All Artists");
            allArtists = new ReactiveList<ArtistViewModel> { allArtistsViewModel };

            Artists = allArtists.CreateDerivedCollection(x => x,
                x => x.IsAllArtists || filteredSongs.Contains(x.Name), (x, y) => x.CompareTo(y), artistUpdateSignal);

            // We need a default sorting order
            ApplyOrder(SortHelpers.GetOrderByArtist<LocalSongViewModel>, ref artistOrder);

            SelectedArtist = allArtistsViewModel;

            var gate = new object();
            Library.SongsUpdated
                .Buffer(TimeSpan.FromSeconds(1), RxApp.TaskpoolScheduler)
                .Where(x => x.Any())
                .Select(_ => Unit.Default)
                .Merge(this.WhenAny(x => x.SearchText, _ => Unit.Default)
                    .Do(_ => SelectedArtist = allArtistsViewModel))
                .Synchronize(gate)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    UpdateSelectableSongs();
                    UpdateArtists();
                });

            this.WhenAnyValue(x => x.SelectedArtist)
                .Skip(1)
                .Synchronize(gate)
                .Subscribe(_ => UpdateSelectableSongs());

            PlayNowCommand = ReactiveCommand.CreateAsyncTask(Library.LocalAccessControl
                .ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin || !coreSettings.LockPlayPause), _ =>
            {
                var songIndex = SelectableSongs.TakeWhile(x => x.Model != SelectedSongs.First().Model).Count();

                return Library.PlayInstantlyAsync(SelectableSongs.Skip(songIndex).Select(x => x.Model), accessToken);
            });

            showAddSongsHelperMessage = Library.SongsUpdated
                .StartWith(Unit.Default)
                .Select(_ => Library.Songs.Count == 0)
                .TakeWhile(x => x)
                .Concat(Observable.Return(false))
                .ToProperty(this, x => x.ShowAddSongsHelperMessage);

            isUpdating = Library.WhenAnyValue(x => x.IsUpdating)
                .ToProperty(this, x => x.IsUpdating);

            OpenTagEditor = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedSongs, x => x.Any()));
        }

        public IReactiveDerivedList<ArtistViewModel> Artists { get; }

        public override DefaultPlaybackAction DefaultPlaybackAction => CoreSettings.DefaultPlaybackAction;

        public int DurationColumnWidth
        {
            get => viewSettings.LocalDurationColumnWidth;
            set => viewSettings.LocalDurationColumnWidth = value;
        }

        public int GenreColumnWidth
        {
            get => viewSettings.LocalGenreColumnWidth;
            set => viewSettings.LocalGenreColumnWidth = value;
        }

        public bool IsUpdating => isUpdating.Value;

        public ReactiveCommand<object> OpenTagEditor { get; }

        public override ReactiveCommand<Unit> PlayNowCommand { get; }

        public ArtistViewModel SelectedArtist
        {
            get => selectedArtist;
            set =>
                // We don't ever want the selected artist to be null
                this.RaiseAndSetIfChanged(ref selectedArtist, value ?? allArtistsViewModel);
        }

        public bool ShowAddSongsHelperMessage => showAddSongsHelperMessage.Value;

        public int TitleColumnWidth
        {
            get => viewSettings.LocalTitleColumnWidth;
            set => viewSettings.LocalTitleColumnWidth = value;
        }

        private void UpdateArtists()
        {
            var groupedByArtist = Library.Songs
                .ToLookup(x => x.Artist, StringComparer.InvariantCultureIgnoreCase);

            var artistsToRemove = allArtists.Where(x => !groupedByArtist.Contains(x.Name)).ToList();
            artistsToRemove.Remove(allArtistsViewModel);

            allArtists.RemoveAll(artistsToRemove);

            foreach (var artistViewModel in artistsToRemove) artistViewModel.Dispose();

            // We use this reverse ordered list of artists so we can priorize the loading of album
            // covers of artists that we display first in the artist list. This way we can "fake" a
            // fast loading of all covers, as the user doesn't see most of the artists down the
            // list. The higher the number, the higher the prioritization.
            var orderedArtists = groupedByArtist.Select(x => x.Key)
                .OrderByDescending(SortHelpers.RemoveArtistPrefixes)
                .ToList();

            foreach (var songs in groupedByArtist)
            {
                var model = allArtists.FirstOrDefault(x =>
                    x.Name.Equals(songs.Key, StringComparison.InvariantCultureIgnoreCase));

                if (model == null)
                {
                    var priority = orderedArtists.IndexOf(songs.Key) + 1;
                    allArtists.Add(new ArtistViewModel(songs.Key, songs, priority));
                }

                else
                {
                    model.UpdateSongs(songs);
                }
            }
        }

        private void UpdateSelectableSongs()
        {
            filteredSongs = Library.Songs.FilterSongs(SearchText)
                .ToLookup(x => x.Artist, StringComparer.InvariantCultureIgnoreCase);

            var newArtists = new HashSet<string>(filteredSongs.Select(x => x.Key));
            var oldArtists = Artists.Where(x => !x.IsAllArtists).Select(x => x.Name);

            if (!newArtists.SetEquals(oldArtists)) artistUpdateSignal.OnNext(Unit.Default);

            var selectableSongs = filteredSongs
                .Where(group =>
                    SelectedArtist.IsAllArtists ||
                    group.Key.Equals(SelectedArtist.Name, StringComparison.InvariantCultureIgnoreCase))
                .SelectMany(x => x)
                .Select(song => new LocalSongViewModel(song))
                .OrderBy(SongOrderFunc)
                .ToList();

            // Ignore redundant song updates.
            if (!selectableSongs.SequenceEqual(SelectableSongs))
            {
                // Scratch the old viewmodels
                foreach (var viewModel in SelectableSongs) viewModel.Dispose();

                SelectableSongs = selectableSongs;
            }

            else
            {
                // We don't have to update the selectable songs, get rid of the redundant ones we've created
                foreach (var viewModel in selectableSongs) viewModel.Dispose();
            }

            if (SelectedSongs == null) SelectedSongs = SelectableSongs.Take(1).ToList();
        }
    }
}