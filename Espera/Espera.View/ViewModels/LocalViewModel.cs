using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
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
        private SortOrder albumOrder;
        private SortOrder artistOrder;
        private SortOrder durationOrder;
        private SortOrder genreOrder;
        private ArtistViewModel selectedArtist;
        private SortOrder titleOrder;

        public LocalViewModel(Library library)
            : base(library)
        {
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
                .Subscribe(_ =>
                {
                    this.UpdateSelectableSongs();
                    this.UpdateArtists();
                });

            this.WhenAny(x => x.SelectedArtist, _ => Unit.Default)
                .Subscribe(_ => this.UpdateSelectableSongs());

            this.playNowCommand = new ReactiveCommand();
            this.PlayNowCommand.Subscribe(p =>
            {
                int songIndex = this.SelectableSongs.TakeWhile(x => x.Model != this.SelectedSongs.First().Model).Count();

                this.Library.PlayInstantly(this.SelectableSongs.Skip(songIndex).Select(x => x.Model));
            });
        }

        public int AlbumColumnWidth
        {
            get { return Settings.Default.LocalAlbumColumnWidth; }
            set { Settings.Default.LocalAlbumColumnWidth = value; }
        }

        public int ArtistColumnWidth
        {
            get { return Settings.Default.LocalArtistColumnWidth; }
            set { Settings.Default.LocalArtistColumnWidth = value; }
        }

        public IEnumerable<ArtistViewModel> Artists
        {
            get { return this.artists; }
        }

        public int DurationColumnWidth
        {
            get { return Settings.Default.LocalDurationColumnWidth; }
            set { Settings.Default.LocalDurationColumnWidth = value; }
        }

        public int GenreColumnWidth
        {
            get { return Settings.Default.LocalGenreColumnWidth; }
            set { Settings.Default.LocalGenreColumnWidth = value; }
        }

        public int PathColumnWidth
        {
            get { return Settings.Default.LocalPathColumnWidth; }
            set { Settings.Default.LocalPathColumnWidth = value; }
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
            get { return Settings.Default.LocalTitleColumnWidth; }
            set { Settings.Default.LocalTitleColumnWidth = value; }
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
            List<ArtistViewModel> artistInfos = this.SelectableSongs
                .GroupBy(song => song.Artist)
                .Select(group =>
                    new ArtistViewModel(group.Key, group.Select(song => song.Album).Distinct().Count(), group.Count()))
                .Concat(new[] { this.allArtistsViewModel })
                .ToList();

            List<ArtistViewModel> artistsToRemove = this.artists
                .Except(artistInfos)
                .ToList();

            foreach (ArtistViewModel artist in artistsToRemove)
            {
                this.artists.Remove(artist);
            }

            foreach (ArtistViewModel artist in artistInfos)
            {
                int index = this.artists.IndexOf(artist);

                if (index != -1)
                {
                    ArtistViewModel updated = this.artists[index];

                    updated.AlbumCount = artist.AlbumCount;
                    updated.SongCount = artist.SongCount;
                }

                else
                {
                    this.artists.Add(artist);
                }
            }

            this.allArtistsViewModel.ArtistCount = this.artists.Count - 1;

            this.artists.Sort();
        }

        private void UpdateSelectableSongs()
        {
            // Restrict this method to one thread at a time, so that updates from the library,
            // which are on a different thread, don't interfere
            this.updateSemaphore.Wait();

            IEnumerable<Song> filtered = this.Library.Songs.FilterSongs(this.SearchText).ToList();

            this.SelectableSongs = filtered
                .Where(song => this.SelectedArtist.IsAllArtists || song.Artist == this.SelectedArtist.Name)
                .Select(song => new LocalSongViewModel(song))
                .OrderBy(this.SongOrderFunc)
                .ToList();

            this.updateSemaphore.Release();
        }
    }
}