using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using MoreLinq;
using Rareform.Patterns.MVVM;
using Rareform.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal sealed class LocalViewModel : SongSourceViewModel<LocalSongViewModel>
    {
        private readonly ArtistViewModel allArtistsViewModel;
        private readonly Dictionary<string, ArtistViewModel> artists;
        private SortOrder albumOrder;
        private SortOrder artistOrder;
        private SortOrder durationOrder;
        private SortOrder genreOrder;
        private string searchText;
        private ArtistViewModel selectedArtist;
        private SortOrder titleOrder;

        public LocalViewModel(Library library)
            : base(library)
        {
            library.Updated += (sender, args) =>
            {
                this.NotifyOfPropertyChange(() => this.Artists);
                this.UpdateSelectableSongs();
            };

            this.StatusViewModel = new StatusViewModel(library);

            this.searchText = String.Empty;

            this.artists = new Dictionary<string, ArtistViewModel>();
            this.allArtistsViewModel = new ArtistViewModel("All Artists");

            // We need a default sorting order
            this.OrderByArtist();

            this.SelectedArtist = this.Artists.First();
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
            get
            {
                return this.artists.Values
                    .OrderBy(artist => RemoveArtistPrefixes(artist.Name, new[] { "A", "The" }))
                    .Prepend(this.allArtistsViewModel);
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

        public ICommand RemoveFromLibraryCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.Library.RemoveFromLibrary(this.SelectedSongs.Select(song => song.Model));

                        this.UpdateSelectableSongs();
                        this.NotifyOfPropertyChange(() => this.Artists);
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

                    this.NotifyOfPropertyChange(() => this.SearchText);

                    this.UpdateSelectableSongs();
                }
            }
        }

        public ArtistViewModel SelectedArtist
        {
            get { return this.selectedArtist; }
            set
            {
                if (value != null && this.SelectedArtist != value)
                {
                    this.selectedArtist = value;

                    this.NotifyOfPropertyChange(() => this.SelectedArtist);

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
                    this.NotifyOfPropertyChange(() => this.Artists);
                }
            };

            this.Library.SongAdded += handler;

            this.StatusViewModel.IsAdding = true;

            this.Library
                .AddLocalSongsAsync(folderPath)
                .ContinueWith(task =>
                {
                    this.Library.SongAdded -= handler;

                    this.NotifyOfPropertyChange(() => this.Artists);
                    this.StatusViewModel.Reset();
                });
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

        /// <example>
        /// With prefixes "A" and "The":
        /// "A Bar" -> "Bar", "The Foos" -> "Foos"
        /// </example>
        private static string RemoveArtistPrefixes(string artistName, IEnumerable<string> prefixes)
        {
            foreach (string s in prefixes)
            {
                int lengthWithSpace = s.Length + 1;

                if (artistName.Length >= lengthWithSpace && artistName.Substring(0, lengthWithSpace) == s + " ")
                {
                    return artistName.Substring(lengthWithSpace);
                }
            }

            return artistName;
        }

        private void UpdateSelectableSongs()
        {
            IEnumerable<Song> filtered = this.Library.Songs.FilterSongs(this.SearchText).ToList();

            var artistInfos = filtered
                .GroupBy(song => song.Artist)
                .Select(group =>
                    new ArtistViewModel(group.Key, group.Select(song => song.Album).Distinct().Count(), group.Count()))
                .ToDictionary(model => model.Name);

            List<string> removableArtists = this.artists
                .Where(pair => !artistInfos.ContainsKey(pair.Key))
                .Select(pair => pair.Key)
                .ToList();

            foreach (string artist in removableArtists)
            {
                this.artists.Remove(artist);
            }

            foreach (ArtistViewModel artist in artistInfos.Values)
            {
                if (this.artists.ContainsKey(artist.Name))
                {
                    ArtistViewModel updated = this.artists[artist.Name];

                    updated.AlbumCount = artist.AlbumCount;
                    updated.SongCount = artist.SongCount;
                }

                else
                {
                    this.artists.Add(artist.Name, artist);
                }
            }

            this.NotifyOfPropertyChange(() => this.Artists);

            this.allArtistsViewModel.ArtistCount = artistInfos.Count;

            this.SelectableSongs = filtered
                .Where(song => this.SelectedArtist.IsAllArtists || song.Artist == this.SelectedArtist.Name)
                .Select(song => new LocalSongViewModel(song))
                .OrderBy(this.SongOrderFunc)
                .ToList();

            this.SelectedSongs = this.SelectableSongs.Take(1);
        }
    }
}