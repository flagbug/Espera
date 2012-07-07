using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Espera.Core;
using Espera.Core.Library;
using Espera.View.Properties;
using Rareform.Patterns.MVVM;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    internal class LocalViewModel : SongSourceViewModel<LocalViewModel>
    {
        private SortOrder albumOrder;
        private SortOrder artistOrder;
        private SortOrder durationOrder;
        private SortOrder genreOrder;
        private SortOrder titleOrder;
        private volatile bool isAdding;
        private string searchText;
        private string selectedArtist;
        private Func<IEnumerable<Song>, IOrderedEnumerable<Song>> songOrderFunc;

        public LocalViewModel(Library library)
            : base(library)
        {
            library.Updated += (sender, args) =>
            {
                this.OnPropertyChanged(vm => vm.Artists);
                this.OnPropertyChanged(vm => vm.SelectableSongs);
            };

            this.StatusViewModel = new StatusViewModel(library);

            this.searchText = String.Empty;

            // We need a default sorting order
            this.OrderByArtist();
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
                    this.OnPropertyChanged(vm => vm.SelectableSongs);
                    this.OnPropertyChanged(vm => vm.Artists);
                }
            }
        }

        public IEnumerable<SongViewModel> SelectableSongs
        {
            get
            {
                // If we are currently adding songs, copy the songs to a new list, so that we don't run into performance issues
                var songs = (this.isAdding ? this.Library.Songs.ToList() : this.Library.Songs)
                    .AsParallel()
                    .Where(song => song.Artist == this.SelectedArtist);

                return songs.FilterSongs(this.SearchText)
                    .OrderBy(this.songOrderFunc)
                    .Select(song => new SongViewModel(song));
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
                    this.OnPropertyChanged(vm => vm.SelectableSongs);
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
            this.songOrderFunc = SortHelpers.GetOrderByAlbum<Song>(this.albumOrder);
            SortHelpers.InverseOrder(ref this.albumOrder);

            this.OnPropertyChanged(vm => vm.SelectableSongs);
        }

        public void OrderByArtist()
        {
            this.songOrderFunc = SortHelpers.GetOrderByArtist<Song>(this.artistOrder);
            SortHelpers.InverseOrder(ref this.artistOrder);

            this.OnPropertyChanged(vm => vm.SelectableSongs);
        }

        public void OrderByDuration()
        {
            this.songOrderFunc = SortHelpers.GetOrderByDuration<Song>(this.durationOrder);
            SortHelpers.InverseOrder(ref this.durationOrder);

            this.OnPropertyChanged(vm => vm.SelectableSongs);
        }

        public void OrderByGenre()
        {
            this.songOrderFunc = SortHelpers.GetOrderByGenre<Song>(this.genreOrder);
            SortHelpers.InverseOrder(ref this.genreOrder);

            this.OnPropertyChanged(vm => vm.SelectableSongs);
        }

        public void OrderByTitle()
        {
            this.songOrderFunc = SortHelpers.GetOrderByTitle<Song>(this.titleOrder);
            SortHelpers.InverseOrder(ref this.titleOrder);

            this.OnPropertyChanged(vm => vm.SelectableSongs);
        }
    }
}