using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Validation;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public class LocalViewModel : SongSourceViewModel<LocalSongViewModel>
    {
        private readonly ReactiveList<ArtistViewModel> allArtists;
        private readonly ArtistViewModel allArtistsViewModel;
        private readonly SortOrder artistOrder;
        private readonly Subject<Unit> artistUpdateSignal;
        private readonly ObservableAsPropertyHelper<bool> isUpdating;
        private readonly ReactiveCommand<Unit> playNowCommand;
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

            this.artistUpdateSignal = new Subject<Unit>();

            this.allArtistsViewModel = new ArtistViewModel("All Artists");
            this.allArtists = new ReactiveList<ArtistViewModel> { this.allArtistsViewModel };

            this.Artists = this.allArtists.CreateDerivedCollection(x => x,
                x => x.IsAllArtists || this.filteredSongs.Contains(x.Name), (x, y) => x.CompareTo(y), this.artistUpdateSignal);

            // We need a default sorting order
            this.ApplyOrder(SortHelpers.GetOrderByArtist<LocalSongViewModel>, ref this.artistOrder);

            this.SelectedArtist = this.allArtistsViewModel;

            var gate = new object();
            this.Library.SongsUpdated
                .Buffer(TimeSpan.FromSeconds(1), RxApp.TaskpoolScheduler)
                .Where(x => x.Any())
                .Select(_ => Unit.Default)
                .Merge(this.WhenAny(x => x.SearchText, _ => Unit.Default)
                    .Do(_ => this.SelectedArtist = this.allArtistsViewModel))
                .Synchronize(gate)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    this.UpdateSelectableSongs();
                    this.UpdateArtists();
                });

            this.WhenAnyValue(x => x.SelectedArtist)
                .Skip(1)
                .Synchronize(gate)
                .Subscribe(_ => this.UpdateSelectableSongs());

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

        public IReactiveDerivedList<ArtistViewModel> Artists { get; private set; }

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

        private void UpdateArtists()
        {
            var groupedByArtist = this.Library.Songs
               .ToLookup(x => x.Artist, StringComparer.InvariantCultureIgnoreCase);

            List<ArtistViewModel> artistsToRemove = this.allArtists.Where(x => !groupedByArtist.Contains(x.Name)).ToList();
            artistsToRemove.Remove(this.allArtistsViewModel);

            this.allArtists.RemoveAll(artistsToRemove);

            foreach (ArtistViewModel artistViewModel in artistsToRemove)
            {
                artistViewModel.Dispose();
            }

            // We use this reverse ordered list of artists so we can priorize the loading of album
            // covers of artists that we display first in the artist list. This way we can "fake" a
            // fast loading of all covers, as the user doesn't see most of the artists down the
            // list. The higher the number, the higher the prioritization.
            List<string> orderedArtists = groupedByArtist.Select(x => x.Key)
                .OrderByDescending(SortHelpers.RemoveArtistPrefixes)
                .ToList();

            foreach (var songs in groupedByArtist)
            {
                ArtistViewModel model = this.allArtists.FirstOrDefault(x => x.Name.Equals(songs.Key, StringComparison.InvariantCultureIgnoreCase));

                if (model == null)
                {
                    int priority = orderedArtists.IndexOf(songs.Key) + 1;
                    this.allArtists.Add(new ArtistViewModel(songs.Key, songs, priority));
                }

                else
                {
                    model.UpdateSongs(songs);
                }
            }
        }

        private void UpdateSelectableSongs()
        {
            this.filteredSongs = this.Library.Songs.FilterSongs(this.SearchText)
                .ToLookup(x => x.Artist, StringComparer.InvariantCultureIgnoreCase);

            var newArtists = new HashSet<string>(this.filteredSongs.Select(x => x.Key));
            var oldArtists = this.Artists.Where(x => !x.IsAllArtists).Select(x => x.Name);

            if (!newArtists.SetEquals(oldArtists))
            {
                this.artistUpdateSignal.OnNext(Unit.Default);
            }

            List<LocalSongViewModel> selectableSongs = this.filteredSongs
                .Where(group => this.SelectedArtist.IsAllArtists || @group.Key.Equals(this.SelectedArtist.Name, StringComparison.InvariantCultureIgnoreCase))
                .SelectMany(x => x)
                .Select(song => new LocalSongViewModel(song))
                .OrderBy(this.SongOrderFunc)
                .ToList();

            // Ignore redundant song updates.
            if (!selectableSongs.SequenceEqual(this.SelectableSongs))
            {
                // Scratch the old viewmodels
                foreach (var viewModel in this.SelectableSongs)
                {
                    viewModel.Dispose();
                }

                this.SelectableSongs = selectableSongs;
            }

            else
            {
                // We don't have to update the selectable songs, get rid of the redundant ones we've created
                foreach (LocalSongViewModel viewModel in selectableSongs)
                {
                    viewModel.Dispose();
                }
            }

            if (this.SelectedSongs == null)
            {
                this.SelectedSongs = this.SelectableSongs.Take(1).ToList();
            }
        }
    }
}