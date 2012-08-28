using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using Rareform.Patterns.MVVM;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    internal sealed class LocalViewModel : SongSourceViewModel<LocalViewModel>
    {
        private SortOrder albumOrder;
        private SortOrder artistOrder;
        private SortOrder durationOrder;
        private SortOrder genreOrder;
        private volatile bool isAdding;
        private string searchText;
        private string selectedArtist;
        private SortOrder titleOrder;

        public LocalViewModel(Library library)
            : base(library)
        {
            library.Updated += (sender, args) =>
            {
                this.OnPropertyChanged(vm => vm.Artists);
                this.UpdateSelectableSongs();
            };

            this.StatusViewModel = new StatusViewModel(library);

            this.searchText = String.Empty;

            // We need a default sorting order
            this.OrderByArtist();

            // Selected the first artist, if there is any (there is always an artist, if there is a song (Unknown Artist))
            if (this.Library.Songs.Any())
            {
                this.SelectedArtist = this.Artists.First();
            }
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

        public IEnumerable<string> Artists
        {
            get
            {
                // If we are currently adding songs, copy the songs to a new list, so that we don't run into performance issues
                IEnumerable<Song> songs = this.isAdding ? this.Library.Songs.ToList() : this.Library.Songs;

                return songs.FilterSongs(this.SearchText)
                    .Where(song => !String.IsNullOrWhiteSpace(song.Artist))
                    .GroupBy(song => song.Artist)
                    .Select(group => group.Key)
                    .OrderBy(artist => artist);
            }
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

        public ICommand RemoveFromLibraryAndPlaylistCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        var songs = this.SelectedSongs.Select(song => song.Model).ToList();

                        this.Library.RemoveFromLibrary(songs);
                        this.Library.RemoveFromPlaylist(songs);

                        this.OnPropertyChanged(vm => vm.SelectableSongs);
                        this.OnPropertyChanged(vm => vm.Artists);
                    },
                    param => this.SelectedSongs != null
                        && this.SelectedSongs.Any()
                        && (this.IsAdmin || !this.Library.LockLibraryRemoval)
                );
            }
        }

        public ICommand RemoveFromLibraryCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.Library.RemoveFromLibrary(this.SelectedSongs.Select(song => song.Model));

                        this.OnPropertyChanged(vm => vm.SelectableSongs);
                        this.OnPropertyChanged(vm => vm.Artists);
                    },
                    param => this.SelectedSongs != null
                        && this.SelectedSongs.Any()
                        && (this.IsAdmin || !this.Library.LockLibraryRemoval)
                );
            }
        }

        public override string SearchText
        {
            get { return this.searchText; }
            set
            {
                if (this.SearchText != value)
                {
                    this.searchText = value;

                    this.OnPropertyChanged(vm => vm.SearchText);

                    this.OnPropertyChanged(vm => vm.Artists);

                    this.UpdateSelectableSongs();
                }
            }
        }

        public string SelectedArtist
        {
            get { return this.selectedArtist; }
            set
            {
                if (this.SelectedArtist != value)
                {
                    this.selectedArtist = value;
                    this.OnPropertyChanged(vm => vm.SelectedArtist);

                    this.UpdateSelectableSongs();
                }
            }
        }

        public StatusViewModel StatusViewModel { get; private set; }

        public int TitleColumnWidth
        {
            get { return Settings.Default.LocalTitleColumnWidth; }
            set { Settings.Default.LocalTitleColumnWidth = value; }
        }

        public void AddSongs(string folderPath)
        {
            if (folderPath == null)
                Throw.ArgumentNullException(() => folderPath);

            string lastArtist = null;

            EventHandler<LibraryFillEventArgs> handler = (sender, e) =>
            {
                this.StatusViewModel.Update(e.Song.OriginalPath, e.ProcessedTagCount, e.TotalTagCount);

                if (e.Song.Artist != lastArtist)
                {
                    lastArtist = e.Song.Artist;
                    this.OnPropertyChanged(vm => vm.Artists);
                }
            };

            this.Library.SongAdded += handler;

            this.isAdding = true;
            this.StatusViewModel.IsAdding = true;

            this.Library
                .AddLocalSongsAsync(folderPath)
                .ContinueWith(task =>
                {
                    this.Library.SongAdded -= handler;

                    this.OnPropertyChanged(vm => vm.Artists);
                    this.isAdding = false;
                    this.StatusViewModel.Reset();
                });
        }

        public void OrderByAlbum()
        {
            this.SongOrderFunc = SortHelpers.GetOrderByAlbum<SongViewModel>(this.albumOrder);
            SortHelpers.InverseOrder(ref this.albumOrder);

            this.ApplyOrder();
        }

        public void OrderByArtist()
        {
            this.SongOrderFunc = SortHelpers.GetOrderByArtist<SongViewModel>(this.artistOrder);
            SortHelpers.InverseOrder(ref this.artistOrder);

            this.ApplyOrder();
        }

        public void OrderByDuration()
        {
            this.SongOrderFunc = SortHelpers.GetOrderByDuration<SongViewModel>(this.durationOrder);
            SortHelpers.InverseOrder(ref this.durationOrder);

            this.ApplyOrder();
        }

        public void OrderByGenre()
        {
            this.SongOrderFunc = SortHelpers.GetOrderByGenre<SongViewModel>(this.genreOrder);
            SortHelpers.InverseOrder(ref this.genreOrder);

            this.ApplyOrder();
        }

        public void OrderByTitle()
        {
            this.SongOrderFunc = SortHelpers.GetOrderByTitle<SongViewModel>(this.titleOrder);
            SortHelpers.InverseOrder(ref this.titleOrder);

            this.ApplyOrder();
        }

        protected override void UpdateSelectableSongs()
        {
            // If we are currently adding songs, copy the songs to a new list, so that we don't run into performance issues
            var songs = (this.isAdding ? this.Library.Songs.ToList() : this.Library.Songs)
                .AsParallel()
                .Where(song => song.Artist == this.SelectedArtist);

            this.SelectableSongs = songs.FilterSongs(this.SearchText)
                .Select(song => new SongViewModel(song))
                .OrderBy(this.SongOrderFunc)
                .ToList();

            this.SelectedSongs = this.SelectableSongs.Take(1);
        }
    }
}