using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using MoreLinq;
using Rareform.Validation;
using ReactiveUI;
using ReactiveUI.Xaml;
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
        private readonly Dictionary<string, ArtistViewModel> artists;
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

            library.IsUpdating.Where(x => x).Subscribe(p =>
            {
                this.RaisePropertyChanged(x => x.Artists);
                this.UpdateSelectableSongs();
            });

            this.StatusViewModel = new StatusViewModel(library);

            this.artists = new Dictionary<string, ArtistViewModel>();
            this.allArtistsViewModel = new ArtistViewModel("All Artists");

            // We need a default sorting order
            this.OrderByArtist();

            this.SelectedArtist = this.Artists.First();

            this.WhenAny(x => x.SearchText, x => x.SelectedArtist, (x1, x2) => Unit.Default)
                .Subscribe(x => this.UpdateSelectableSongs());

            IObservable<bool> canRemoveFromLibrary = this
                .WhenAny(x => x.SelectedSongs, x => x.Value)
                .CombineLatest(this.Library.AccessMode,
                    (selectedSongs, accessMode) =>
                        selectedSongs != null && selectedSongs.Any() &&
                        (this.IsAdmin || !this.Library.LockLibraryRemoval));

            this.RemoveFromLibraryCommand = new ReactiveCommand(canRemoveFromLibrary);
            this.RemoveFromLibraryCommand.Subscribe(p =>
            {
                this.Library.RemoveFromLibrary(this.SelectedSongs.Select(song => song.Model));

                this.UpdateSelectableSongs();
            });

            this.PlayNowCommand = new ReactiveCommand();
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

        public IReactiveCommand PlayNowCommand { get; private set; }

        public IReactiveCommand RemoveFromLibraryCommand { get; private set; }

        public ArtistViewModel SelectedArtist
        {
            get { return this.selectedArtist; }
            set { this.RaiseAndSetIfChanged(value); }
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
                    this.RaisePropertyChanged(x => x.Artists);
                }
            };

            this.Library.SongAdded += handler;

            this.StatusViewModel.IsAdding = true;

            IDisposable d = Observable.Interval(TimeSpan.FromSeconds(1.5))
                .Subscribe(p => this.UpdateSelectableSongs());

            this.Library
                .AddLocalSongsAsync(folderPath)
                .ContinueWith(task =>
                {
                    this.Library.SongAdded -= handler;

                    d.Dispose();

                    this.UpdateSelectableSongs();
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
            // Restrict this method to one thread at a time, so that updates from the library,
            // which are on a different thread, don't interfere
            this.updateSemaphore.Wait();

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
                ArtistViewModel updated;

                if (this.artists.TryGetValue(artist.Name, out updated))
                {
                    updated.AlbumCount = artist.AlbumCount;
                    updated.SongCount = artist.SongCount;
                }

                else
                {
                    this.artists.Add(artist.Name, artist);
                }
            }

            this.RaisePropertyChanged(x => x.Artists);

            this.allArtistsViewModel.ArtistCount = artistInfos.Count;

            this.SelectableSongs = filtered
                .Where(song => this.SelectedArtist.IsAllArtists || song.Artist == this.SelectedArtist.Name)
                .Select(song => new LocalSongViewModel(song))
                .OrderBy(this.SongOrderFunc)
                .ToList();

            this.SelectedSongs = this.SelectableSongs.Take(1);

            this.updateSemaphore.Release();
        }
    }
}