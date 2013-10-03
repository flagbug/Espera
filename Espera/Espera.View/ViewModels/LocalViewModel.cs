using Espera.Core.Management;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;

namespace Espera.View.ViewModels
{
    internal sealed class LocalViewModel : SongSourceViewModel<LocalSongViewModel>
    {
        private readonly ArtistViewModel allArtistsViewModel;
        private readonly ReactiveList<ArtistViewModel> artists;
        private readonly IReactiveCommand playNowCommand;
        private readonly SemaphoreSlim updateSemaphore;
        private readonly ViewSettings viewSettings;
        private SortOrder albumOrder;
        private SortOrder artistOrder;
        private SortOrder durationOrder;
        private List<LocalSongViewModel> filteredSongs;
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

            this.allArtistsViewModel = new ArtistViewModel("All Artists");
            this.artists = new ReactiveList<ArtistViewModel> { this.allArtistsViewModel };

            // We need a default sorting order
            this.OrderByArtist();

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

        public int AlbumColumnWidth
        {
            get { return this.viewSettings.LocalAlbumColumnWidth; }
            set { this.viewSettings.LocalAlbumColumnWidth = value; }
        }

        public int ArtistColumnWidth
        {
            get { return this.viewSettings.LocalArtistColumnWidth; }
            set { this.viewSettings.LocalArtistColumnWidth = value; }
        }

        public IEnumerable<ArtistViewModel> Artists
        {
            get { return this.artists; }
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

        public int PathColumnWidth
        {
            get { return this.viewSettings.LocalPathColumnWidth; }
            set { this.viewSettings.LocalPathColumnWidth = value; }
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

        public void OrderByAlbum()
        {
            this.ApplyOrder(SortHelpers.GetOrderByAlbum<LocalSongViewModel>, ref this.albumOrder);
        }

        public void OrderByArtist()
        {
            this.ApplyOrder(SortHelpers.GetOrderByArtist<LocalSongViewModel>, ref this.artistOrder);
        }

        public void OrderByDuration()
        {
            this.ApplyOrder(SortHelpers.GetOrderByDuration<LocalSongViewModel>, ref this.durationOrder);
        }

        public void OrderByGenre()
        {
            this.ApplyOrder(SortHelpers.GetOrderByGenre<LocalSongViewModel>, ref this.genreOrder);
        }

        public void OrderByTitle()
        {
            this.ApplyOrder(SortHelpers.GetOrderByTitle<LocalSongViewModel>, ref this.titleOrder);
        }

        private void UpdateArtists()
        {
            var groupedByArtist = this.filteredSongs
               .AsParallel()
               .ToLookup(x => x.Artist, StringComparer.InvariantCultureIgnoreCase);

            List<ArtistViewModel> artistsToRemove = this.artists.Where(x => !groupedByArtist.Contains(x.Name)).ToList();
            artistsToRemove.Remove(this.allArtistsViewModel);

            this.artists.RemoveAll(artistsToRemove);

            foreach (var songs in groupedByArtist)
            {
                ArtistViewModel model = this.artists.FirstOrDefault(x => x.Name.Equals(songs.Key, StringComparison.InvariantCultureIgnoreCase));

                if (model == null)
                {
                    this.artists.Add(new ArtistViewModel(songs));
                }

                else
                {
                    model.Songs = songs;
                }
            }

            this.artists.Sort();
        }

        private void UpdateSelectableSongs()
        {
            // Restrict this method to one thread at a time, so that updates from the library,
            // which are on a different thread, don't interfere
            this.updateSemaphore.Wait();

            this.filteredSongs = this.Library.Songs.FilterSongs(this.SearchText).Select(song => new LocalSongViewModel(song)).ToList();

            this.SelectableSongs = this.filteredSongs
                .AsParallel()
                .Where(song => this.SelectedArtist.IsAllArtists || song.Artist.Equals(this.SelectedArtist.Name, StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(this.SongOrderFunc)
                .ToList();

            this.updateSemaphore.Release();
        }
    }
}