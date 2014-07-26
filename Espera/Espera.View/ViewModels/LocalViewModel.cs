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
using ReactiveUI.Legacy;

namespace Espera.View.ViewModels
{
    public sealed class LocalViewModel : SongSourceViewModel<LocalSongViewModel>
    {
        private readonly ReactiveList<ArtistViewModel> allArtists;
        private readonly ArtistViewModel allArtistsViewModel;
        private readonly Subject<Unit> artistUpdateSignal;
        private readonly object gate;
        private readonly ObservableAsPropertyHelper<bool> isUpdating;
        private readonly ReactiveUI.Legacy.ReactiveCommand playNowCommand;
        private readonly ObservableAsPropertyHelper<bool> showAddSongsHelperMessage;
        private readonly ViewSettings viewSettings;
        private SortOrder artistOrder;
        private ILookup<string, Song> filteredSongs;
        private ArtistViewModel selectedArtist;

        public LocalViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, Guid accessToken)
            : base(library, accessToken)
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            this.artistUpdateSignal = new Subject<Unit>();
            this.gate = new object();

            this.allArtistsViewModel = new ArtistViewModel("All Artists");
            this.allArtists = new ReactiveList<ArtistViewModel> { this.allArtistsViewModel };

            this.Artists = this.allArtists.CreateDerivedCollection(x => x,
                x => x.IsAllArtists || this.filteredSongs.Contains(x.Name), (x, y) => x.CompareTo(y), this.artistUpdateSignal);

            // We need a default sorting order
            this.ApplyOrder(SortHelpers.GetOrderByArtist<LocalSongViewModel>, ref this.artistOrder);

            this.SelectedArtist = this.allArtistsViewModel;

            this.Library.SongsUpdated
                .Buffer(TimeSpan.FromSeconds(1), RxApp.TaskpoolScheduler)
                .Where(x => x.Any())
                .Select(_ => Unit.Default)
                .Merge(this.WhenAny(x => x.SearchText, _ => Unit.Default)
                    .Do(_ => this.SelectedArtist = this.allArtistsViewModel))
                .Synchronize(this.gate)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    this.UpdateSelectableSongs();
                    this.UpdateArtists();
                });

            this.WhenAnyValue(x => x.SelectedArtist)
                .Skip(1)
                .Synchronize(this.gate)
                .Subscribe(_ => this.UpdateSelectableSongs());

            this.playNowCommand = new ReactiveUI.Legacy.ReactiveCommand(this.Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin || !coreSettings.LockPlayPause));
            this.PlayNowCommand.RegisterAsyncTask(_ =>
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

            this.isUpdating = this.Library.IsUpdating.ToProperty(this, x => x.IsUpdating);
        }

        public IReactiveDerivedList<ArtistViewModel> Artists { get; private set; }

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

        public override ReactiveUI.Legacy.ReactiveCommand PlayNowCommand
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
               .AsParallel()
               .ToLookup(x => x.Artist, StringComparer.InvariantCultureIgnoreCase);

            List<ArtistViewModel> artistsToRemove = this.allArtists.Where(x => !groupedByArtist.Contains(x.Name)).ToList();
            artistsToRemove.Remove(this.allArtistsViewModel);

            this.allArtists.RemoveAll(artistsToRemove);

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

                List<IObservable<string>> artworkKeys = songs
                    .Select(x => x.ArtworkKey)
                    .ToList();

                if (model == null)
                {
                    int priority = orderedArtists.IndexOf(songs.Key) + 1;
                    this.allArtists.Add(new ArtistViewModel(songs.Key, artworkKeys, priority));
                }

                else
                {
                    model.UpdateArtwork(artworkKeys);
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
                this.SelectableSongs = selectableSongs;
            }

            if (this.SelectedSongs == null)
            {
                this.SelectedSongs = this.SelectableSongs.Take(1).ToList();
            }
        }
    }
}