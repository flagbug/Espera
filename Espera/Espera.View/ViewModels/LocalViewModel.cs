using Espera.Core;
using Espera.Core.Management;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Espera.View.ViewModels
{
    internal sealed class LocalViewModel : SongSourceViewModel<LocalSongViewModel>
    {
        private readonly ReactiveList<ArtistViewModel> allArtists;
        private readonly ArtistViewModel allArtistsViewModel;
        private readonly Subject<Unit> artistUpdateSignal;
        private readonly IReactiveCommand playNowCommand;
        private readonly SemaphoreSlim updateSemaphore;
        private readonly ViewSettings viewSettings;
        private SortOrder albumOrder;
        private SortOrder artistOrder;
        private SortOrder durationOrder;
        private ILookup<string, LocalSongViewModel> filteredSongs;
        private SortOrder genreOrder;
        private ArtistViewModel selectedArtist;
        private SortOrder titleOrder;

        public LocalViewModel(Library library, ViewSettings viewSettings)
            : base(library)
        {
            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            this.viewSettings = viewSettings;

            this.updateSemaphore = new SemaphoreSlim(1, 1);
            this.artistUpdateSignal = new Subject<Unit>();

            this.allArtistsViewModel = new ArtistViewModel("All Artists");
            this.allArtists = new ReactiveList<ArtistViewModel> { this.allArtistsViewModel };

            this.Artists = this.allArtists.CreateDerivedCollection(x => x,
                x => x.IsAllArtists || this.filteredSongs.Contains(x.Name), (x, y) => x.CompareTo(y), this.artistUpdateSignal);

            // We need a default sorting order
            this.ApplyOrder(SortHelpers.GetOrderByArtist<LocalSongViewModel>, ref this.artistOrder);

            this.SelectedArtist = this.allArtistsViewModel;

            this.Library.SongsUpdated
                .Buffer(TimeSpan.FromSeconds(1))
                .Where(x => x.Any())
                .Select(_ => Unit.Default)
                .Merge(this.WhenAny(x => x.SearchText, _ => Unit.Default)
                    .Do(_ => this.SelectedArtist = this.allArtistsViewModel))
                .SubscribeOn(RxApp.MainThreadScheduler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    this.UpdateSelectableSongs();
                    this.UpdateArtists();
                });

            this.WhenAnyValue(x => x.SelectedArtist)
                .Subscribe(_ => this.UpdateSelectableSongs());

            this.playNowCommand = new ReactiveCommand();
            this.PlayNowCommand.RegisterAsyncTask(_ =>
            {
                int songIndex = this.SelectableSongs.TakeWhile(x => x.Model != this.SelectedSongs.First().Model).Count();

                return this.Library.PlayInstantlyAsync(this.SelectableSongs.Skip(songIndex).Select(x => x.Model));
            });
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

        public override IReactiveCommand PlayNowCommand
        {
            get { return this.playNowCommand; }
        }

        public ArtistViewModel SelectedArtist
        {
            get { return this.selectedArtist; }
            set { this.RaiseAndSetIfChanged(ref this.selectedArtist, value); }
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

            foreach (var songs in groupedByArtist)
            {
                ArtistViewModel model = this.allArtists.FirstOrDefault(x => x.Name.Equals(songs.Key, StringComparison.InvariantCultureIgnoreCase));

                List<IObservable<string>> artworkKeys = songs.Cast<LocalSong>()
                    .Select(x => x.ArtworkKey)
                    .ToList();

                if (model == null)
                {
                    this.allArtists.Add(new ArtistViewModel(songs.Key, artworkKeys));
                }

                else
                {
                    model.UpdateArtwork(artworkKeys);
                }
            }
        }

        private void UpdateSelectableSongs()
        {
            // Restrict this method to one thread at a time, so that updates from the library,
            // which are on a different thread, don't interfere
            this.updateSemaphore.Wait();

            this.filteredSongs = this.Library.Songs.FilterSongs(this.SearchText)
                .Select(song => new LocalSongViewModel(song))
                .ToLookup(x => x.Artist, StringComparer.InvariantCultureIgnoreCase);

            this.artistUpdateSignal.OnNext(Unit.Default);

            this.SelectableSongs = this.filteredSongs
                .AsParallel()
                .Where(group => this.SelectedArtist.IsAllArtists || group.Key.Equals(this.SelectedArtist.Name, StringComparison.InvariantCultureIgnoreCase))
                .SelectMany(x => x)
                .OrderBy(this.SongOrderFunc)
                .ToList();

            this.updateSemaphore.Release();
        }
    }
}