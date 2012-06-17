using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using Espera.Core;
using Espera.Core.Library;
using Espera.View.Properties;
using Rareform.Patterns.MVVM;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    internal class MainViewModel : ViewModelBase<MainViewModel>, IDisposable
    {
        private readonly Library library;
        private readonly Timer playlistTimeoutUpdateTimer;
        private readonly Timer updateTimer;
        private bool displayTimeoutWarning;
        private volatile bool isAdding;
        private bool isLocal;
        private bool isYoutube;
        private string searchText;
        private string selectedArtist;
        private IEnumerable<PlaylistEntryViewModel> selectedPlaylistEntries;
        private IEnumerable<SongViewModel> selectedSongs;
        private Func<IEnumerable<Song>, IOrderedEnumerable<Song>> localSongOrderFunc;
        private SortOrder currentLocalSongTitleOrder;
        private SortOrder currentLocalSongArtistOrder;
        private SortOrder currentLocalSongAlbumOrder;
        private SortOrder currentLocalSongDurationOrder;
        private SortOrder currentLocalSongGenreOrder;
        private Func<IEnumerable<YoutubeSong>, IOrderedEnumerable<YoutubeSong>> youtubeSongOrderFunc;
        private SortOrder currentYoutubeSongTitleOrder;
        private SortOrder currentYoutubeSongDurationOrder;
        private SortOrder currentYoutubeSongRatingOrder;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel()
        {
            this.library = new Library();
            this.library.SongStarted += LibraryRaisedSongStarted;
            this.library.SongFinished += LibraryRaisedSongFinished;
            this.library.AccessModeChanged += (sender, e) => this.UpdateUserAccess();
            this.library.Updated += (sender, args) =>
            {
                this.OnPropertyChanged(vm => vm.Artists);
                this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
            };

            this.AdministratorViewModel = new AdministratorViewModel(this.library);
            this.StatusViewModel = new StatusViewModel(this.library);

            this.updateTimer = new Timer(333);
            this.updateTimer.Elapsed += (sender, e) => this.UpdateCurrentTime();

            this.playlistTimeoutUpdateTimer = new Timer(333);
            this.playlistTimeoutUpdateTimer.Elapsed += (sender, e) => this.UpdateRemainingPlaylistTimeout();
            this.playlistTimeoutUpdateTimer.Start();

            this.searchText = String.Empty;

            // We need a default sorting order
            this.OrderLocalSongsByArtist();
            this.OrderYoutubeSongsByTitle();
        }

        public ICommand AddSelectedSongsToPlaylistCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        if (!this.library.CanAddSongToPlaylist)
                        {
                            // Trigger the animation
                            this.DisplayTimeoutWarning = true;
                            this.DisplayTimeoutWarning = false;

                            return;
                        }

                        if (this.IsAdmin)
                        {
                            this.library.AddSongsToPlaylist(this.SelectedSongs.Select(song => song.Model));
                        }

                        else
                        {
                            this.library.AddSongToPlaylist(this.SelectedSongs.Select(song => song.Model).Single());
                            this.OnPropertyChanged(vm => vm.RemainingPlaylistTimeout);
                        }

                        this.OnPropertyChanged(vm => vm.Playlist);
                        this.OnPropertyChanged(vm => vm.SongsRemaining);
                        this.OnPropertyChanged(vm => vm.TimeRemaining);
                    },
                    param => this.SelectedSongs != null && this.SelectedSongs.Any()
                );
            }
        }

        public AdministratorViewModel AdministratorViewModel { get; private set; }

        public IEnumerable<string> Artists
        {
            get
            {
                // If we are currently adding songs, copy the songs to a new list, so that we don't run into performance issues
                IEnumerable<Song> songs = this.isAdding ? this.library.Songs.ToList() : this.library.Songs;

                return songs.FilterSongs(this.SearchText)
                    .Where(song => !String.IsNullOrWhiteSpace(song.Artist))
                    .GroupBy(song => song.Artist)
                    .Select(group => group.Key)
                    .OrderBy(artist => artist);
            }
        }

        public bool CanChangeTime
        {
            get { return this.library.CanChangeTime; }
        }

        public bool CanChangeVolume
        {
            get { return this.library.CanChangeVolume; }
        }

        public bool CanUseYoutube
        {
            get { return !this.library.StreamYoutube || RegistryHelper.IsVlcInstalled(); }
        }

        public int CurrentSeconds
        {
            get { return (int)this.CurrentTime.TotalSeconds; }
            set { this.library.CurrentTime = TimeSpan.FromSeconds(value); }
        }

        public TimeSpan CurrentTime
        {
            get { return this.library.CurrentTime; }
        }

        public bool DisplayTimeoutWarning
        {
            get { return this.displayTimeoutWarning; }
            set
            {
                if (this.displayTimeoutWarning != value)
                {
                    this.displayTimeoutWarning = value;
                    this.OnPropertyChanged(vm => vm.DisplayTimeoutWarning);
                }
            }
        }

        public bool IsAdmin
        {
            get { return this.library.AccessMode == AccessMode.Administrator; }
        }

        public bool IsLocal
        {
            get { return this.isLocal; }
            set
            {
                if (this.IsLocal != value)
                {
                    this.isLocal = value;
                    this.OnPropertyChanged(vm => vm.IsLocal);

                    if (this.IsLocal)
                    {
                        this.SearchText = String.Empty;
                    }
                }
            }
        }

        public bool IsPlaying
        {
            get { return this.library.IsPlaying; }
        }

        public bool IsYoutube
        {
            get { return this.isYoutube; }
            set
            {
                if (this.IsYoutube != value)
                {
                    this.isYoutube = value;
                    this.OnPropertyChanged(vm => vm.IsYoutube);

                    if (this.IsYoutube)
                    {
                        this.SearchText = String.Empty;
                    }
                }
            }
        }

        public int LocalAlbumColumnWidth
        {
            get { return Settings.Default.LocalAlbumColumnWidth; }
            set { Settings.Default.LocalAlbumColumnWidth = value; }
        }

        public int LocalArtistColumnWidth
        {
            get { return Settings.Default.LocalArtistColumnWidth; }
            set { Settings.Default.LocalArtistColumnWidth = value; }
        }

        public int LocalDurationColumnWidth
        {
            get { return Settings.Default.LocalDurationColumnWidth; }
            set { Settings.Default.LocalDurationColumnWidth = value; }
        }

        public int LocalGenreColumnWidth
        {
            get { return Settings.Default.LocalGenreColumnWidth; }
            set { Settings.Default.LocalGenreColumnWidth = value; }
        }

        public int LocalPathColumnWidth
        {
            get { return Settings.Default.LocalPathColumnWidth; }
            set { Settings.Default.LocalPathColumnWidth = value; }
        }

        public int LocalTitleColumnWidth
        {
            get { return Settings.Default.LocalTitleColumnWidth; }
            set { Settings.Default.LocalTitleColumnWidth = value; }
        }

        /// <summary>
        /// Sets the volume to the lowest possible value.
        /// </summary>
        public ICommand MuteCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.Volume = 0,
                    param => this.IsAdmin
                );
            }
        }

        /// <summary>
        /// Plays the next song in the playlist.
        /// </summary>
        public ICommand NextSongCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.library.PlayNextSong(),
                    param => this.IsAdmin && this.library.CanPlayNextSong
                );
            }
        }

        /// <summary>
        /// Pauses the currently played song.
        /// </summary>
        public ICommand PauseCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.library.PauseSong();
                        this.updateTimer.Stop();
                        this.OnPropertyChanged(vm => vm.IsPlaying);
                    },
                    param => this.IsAdmin || !this.library.LockPlayPause
                );
            }
        }

        /// <summary>
        /// Plays the song that is currently selected in the playlist or continues the song if it is paused.
        /// </summary>
        public ICommand PlayCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        /*
                         * Some callers want to ignore if a song is paused and want to play the currently
                         * selected song, instead of continuing the currently paused song.
                         * This decision is passed as parameter.
                         *
                         * XAML commands pass the parameter as string, code-behind commands pass the parameter
                         * as boolean, so we have to check what type it is.
                         */
                        var value = param as string;

                        bool ignorePause = value == null ? (bool)param : Boolean.Parse(value);

                        if (this.library.IsPaused && !ignorePause)
                        {
                            this.library.ContinueSong();
                            this.updateTimer.Start();
                            this.OnPropertyChanged(vm => vm.IsPlaying);
                        }

                        else
                        {
                            this.library.PlaySong(this.SelectedPlaylistEntries.First().Index);
                        }
                    },
                    param => (this.IsAdmin || !this.library.LockPlayPause) &&
                        ((this.SelectedPlaylistEntries != null && this.SelectedPlaylistEntries.Count() == 1) ||
                        (this.library.LoadedSong != null && this.library.IsPaused))
                );
            }
        }

        public IEnumerable<PlaylistEntryViewModel> Playlist
        {
            get
            {
                var playlist = this.library.Playlist
                    .Select((song, index) => new PlaylistEntryViewModel(song, index))
                    .ToList(); // We want a list, so that ReSharper doesn't complain about multiple enumerations

                if (this.library.CurrentSongIndex.HasValue)
                {
                    playlist[this.library.CurrentSongIndex.Value].IsPlaying = true;

                    // If there are more than 5 songs from the beginning of the playlist to the current played song,
                    // skip all, but 5 songs to the position of the currently played song
                    if (playlist.TakeWhile(song => !song.IsPlaying).Count() > 5)
                    {
                        playlist = playlist.Skip(this.library.CurrentSongIndex.Value - 5).ToList();
                    }

                    foreach (var model in playlist.TakeWhile(song => !song.IsPlaying))
                    {
                        model.IsInactive = true;
                    }
                }

                return playlist;
            }
        }

        public int PlaylistAlbumColumnWidth
        {
            get { return Settings.Default.PlaylistAlbumColumnWidth; }
            set { Settings.Default.PlaylistAlbumColumnWidth = value; }
        }

        public int PlaylistArtistColumnWidth
        {
            get { return Settings.Default.PlaylistArtistColumnWidth; }
            set { Settings.Default.PlaylistArtistColumnWidth = value; }
        }

        public int PlaylistCachingProgressColumnWidth
        {
            get { return Settings.Default.PlaylistCachingProgressColumnWidth; }
            set { Settings.Default.PlaylistCachingProgressColumnWidth = value; }
        }

        public int PlaylistDurationColumnWidth
        {
            get { return Settings.Default.PlaylistDurationColumnWidth; }
            set { Settings.Default.PlaylistDurationColumnWidth = value; }
        }

        public int PlaylistGenreColumnWidth
        {
            get { return Settings.Default.PlaylistGenreColumnWidth; }
            set { Settings.Default.PlaylistGenreColumnWidth = value; }
        }

        // Save the playlist height as string, so that the initial value can be "*"
        public string PlaylistHeight
        {
            get { return Settings.Default.PlaylistHeight; }
            set { Settings.Default.PlaylistHeight = value; }
        }

        public int PlaylistSourceColumnWidth
        {
            get { return Settings.Default.PlaylistSourceColumnWidth; }
            set { Settings.Default.PlaylistSourceColumnWidth = value; }
        }

        public int PlaylistTitleColumnWidth
        {
            get { return Settings.Default.PlaylistTitleColumnWidth; }
            set { Settings.Default.PlaylistTitleColumnWidth = value; }
        }

        /// <summary>
        /// Plays the song that is before the currently played song in the playlist.
        /// </summary>
        public ICommand PreviousSongCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.library.PlayPreviousSong(),
                    param => this.IsAdmin && this.library.CanPlayPreviousSong
                );
            }
        }

        public TimeSpan RemainingPlaylistTimeout
        {
            get { return this.library.RemainingPlaylistTimeout; }
        }

        public ICommand RemoveSelectedPlaylistEntriesCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.library.RemoveFromPlaylist(this.SelectedPlaylistEntries.Select(entry => entry.Index));

                        this.OnPropertyChanged(vm => vm.Playlist);
                        this.OnPropertyChanged(vm => vm.SongsRemaining);
                        this.OnPropertyChanged(vm => vm.TimeRemaining);
                    },
                    param => this.SelectedPlaylistEntries != null
                        && this.SelectedPlaylistEntries.Any()
                        && (this.IsAdmin || !this.library.LockPlaylistRemoval)
                );
            }
        }

        public object RemoveSelectedSongsFromLibraryAndPlaylistCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        var songs = this.SelectedSongs.Select(song => song.Model).ToList();

                        this.library.RemoveFromLibrary(songs);
                        this.library.RemoveFromPlaylist(songs);

                        this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
                        this.OnPropertyChanged(vm => vm.Playlist);
                        this.OnPropertyChanged(vm => vm.SongsRemaining);
                        this.OnPropertyChanged(vm => vm.TimeRemaining);
                        this.OnPropertyChanged(vm => vm.Artists);
                    },
                    param => this.SelectedSongs != null
                        && this.SelectedSongs.Any()
                        && (this.IsAdmin || !this.library.LockLibraryRemoval)
                );
            }
        }

        public ICommand RemoveSelectedSongsFromLibraryCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.library.RemoveFromLibrary(this.SelectedSongs.Select(song => song.Model));

                        this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
                        this.OnPropertyChanged(vm => vm.Playlist);
                        this.OnPropertyChanged(vm => vm.SongsRemaining);
                        this.OnPropertyChanged(vm => vm.TimeRemaining);
                        this.OnPropertyChanged(vm => vm.Artists);
                    },
                    param => this.SelectedSongs != null
                        && this.SelectedSongs.Any()
                        && (this.IsAdmin || !this.library.LockLibraryRemoval)
                );
            }
        }

        public string SearchText
        {
            get { return this.searchText; }
            set
            {
                if (this.SearchText != value)
                {
                    this.searchText = value;
                    this.OnPropertyChanged(vm => vm.SearchText);

                    if (this.IsLocal)
                    {
                        this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
                        this.OnPropertyChanged(vm => vm.Artists);
                    }
                }
            }
        }

        public IEnumerable<SongViewModel> SelectableLocalSongs
        {
            get
            {
                // If we are currently adding songs, copy the songs to a new list, so that we don't run into performance issues
                var songs = (this.isAdding ? this.library.Songs.ToList() : this.library.Songs)
                    .AsParallel()
                    .Where(song => song.Artist == this.SelectedArtist);

                return songs.FilterSongs(this.SearchText)
                    .OrderBy(this.localSongOrderFunc)
                    .Select(song => new SongViewModel(song));
            }
        }

        public IEnumerable<SongViewModel> SelectableYoutubeSongs
        {
            get
            {
                var finder = new YoutubeSongFinder(this.SearchText);
                finder.Start();

                return finder.SongsFound
                    .OrderBy(this.youtubeSongOrderFunc)
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
                    this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
                    this.IsLocal = true;
                    this.IsYoutube = false;
                }
            }
        }

        public IEnumerable<PlaylistEntryViewModel> SelectedPlaylistEntries
        {
            get { return this.selectedPlaylistEntries; }
            set
            {
                if (this.SelectedPlaylistEntries != value)
                {
                    this.selectedPlaylistEntries = value;
                    this.OnPropertyChanged(vm => vm.SelectedPlaylistEntries);
                    this.OnPropertyChanged(vm => vm.PlayCommand);
                }
            }
        }

        public IEnumerable<SongViewModel> SelectedSongs
        {
            get { return this.selectedSongs; }
            set
            {
                if (this.SelectedSongs != value)
                {
                    this.selectedSongs = value;
                    this.OnPropertyChanged(vm => vm.SelectedSongs);
                }
            }
        }

        /// <summary>
        /// Gets the number of songs that come after the currently played song.
        /// </summary>
        public int SongsRemaining
        {
            get
            {
                return this.Playlist
                    .SkipWhile(song => song.IsInactive)
                    .Count();
            }
        }

        public StatusViewModel StatusViewModel { get; private set; }

        /// <summary>
        /// Gets the total remaining time of all songs that come after the currently played song.
        /// </summary>
        public TimeSpan? TimeRemaining
        {
            get
            {
                var songs = this.Playlist
                    .SkipWhile(song => song.IsInactive)
                    .ToList();

                if (songs.Any())
                {
                    return songs
                        .Select(song => song.Duration)
                        .Aggregate((t1, t2) => t1 + t2);
                }

                return null;
            }
        }

        public int TotalSeconds
        {
            get { return (int)this.TotalTime.TotalSeconds; }
        }

        public TimeSpan TotalTime
        {
            get { return this.library.TotalTime; }
        }

        /// <summary>
        /// Sets the volume to the highest possible value.
        /// </summary>
        public ICommand UnMuteCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.Volume = 1,
                    param => this.IsAdmin
                );
            }
        }

        public double Volume
        {
            get { return this.library.Volume; }
            set
            {
                this.library.Volume = (float)value;
                this.OnPropertyChanged(vm => vm.Volume);
            }
        }

        public int YoutubeDurationColumnWidth
        {
            get { return Settings.Default.YoutubeDurationColumnWidth; }
            set { Settings.Default.YoutubeDurationColumnWidth = value; }
        }

        public int YoutubeLinkColumnWidth
        {
            get { return Settings.Default.YoutubeLinkColumnWidth; }
            set { Settings.Default.YoutubeLinkColumnWidth = value; }
        }

        public int YoutubeRatingColumnWidth
        {
            get { return Settings.Default.YoutubeRatingColumnWidth; }
            set { Settings.Default.YoutubeRatingColumnWidth = value; }
        }

        public int YoutubeTitleColumnWidth
        {
            get { return Settings.Default.YoutubeTitleColumnWidth; }
            set { Settings.Default.YoutubeTitleColumnWidth = value; }
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

            this.library.SongAdded += handler;

            this.isAdding = true;
            this.StatusViewModel.IsAdding = true;

            this.library
                .AddLocalSongsAsync(folderPath)
                .ContinueWith(task =>
                {
                    this.library.SongAdded -= handler;

                    this.OnPropertyChanged(vm => vm.Artists);
                    this.isAdding = false;
                    this.StatusViewModel.Reset();
                });
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.library.Dispose();
            this.updateTimer.Dispose();
        }

        public void OrderLocalSongsByTitle()
        {
            this.localSongOrderFunc = SortHelpers.GetOrderByTitle<Song>(this.currentLocalSongTitleOrder);
            SortHelpers.InverseOrder(ref this.currentLocalSongTitleOrder);

            this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
        }

        public void OrderLocalSongsByArtist()
        {
            this.localSongOrderFunc = SortHelpers.GetOrderByArtist<Song>(this.currentLocalSongArtistOrder);
            SortHelpers.InverseOrder(ref this.currentLocalSongArtistOrder);

            this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
        }

        public void OrderLocalSongsByAlbum()
        {
            this.localSongOrderFunc = SortHelpers.GetOrderByAlbum<Song>(this.currentLocalSongAlbumOrder);
            SortHelpers.InverseOrder(ref this.currentLocalSongAlbumOrder);

            this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
        }

        public void OrderLocalSongsByDuration()
        {
            this.localSongOrderFunc = SortHelpers.GetOrderByDuration<Song>(this.currentLocalSongDurationOrder);
            SortHelpers.InverseOrder(ref this.currentLocalSongDurationOrder);

            this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
        }

        public void OrderLocalSongsByGenre()
        {
            this.localSongOrderFunc = SortHelpers.GetOrderByGenre<Song>(this.currentLocalSongGenreOrder);
            SortHelpers.InverseOrder(ref this.currentLocalSongGenreOrder);

            this.OnPropertyChanged(vm => vm.SelectableLocalSongs);
        }

        public void OrderYoutubeSongsByTitle()
        {
            this.youtubeSongOrderFunc = SortHelpers.GetOrderByTitle<YoutubeSong>(this.currentYoutubeSongTitleOrder);
            SortHelpers.InverseOrder(ref this.currentYoutubeSongTitleOrder);

            this.OnPropertyChanged(vm => vm.SelectableYoutubeSongs);
        }

        public void OrderYoutubeSongsByDuration()
        {
            this.youtubeSongOrderFunc = SortHelpers.GetOrderByDuration<YoutubeSong>(this.currentYoutubeSongDurationOrder);
            SortHelpers.InverseOrder(ref this.currentYoutubeSongDurationOrder);

            this.OnPropertyChanged(vm => vm.SelectableYoutubeSongs);
        }

        public void OrderYoutubeSongsByRating()
        {
            this.youtubeSongOrderFunc = SortHelpers.GetOrderByRating(this.currentYoutubeSongRatingOrder);
            SortHelpers.InverseOrder(ref this.currentYoutubeSongRatingOrder);

            this.OnPropertyChanged(vm => vm.SelectableYoutubeSongs);
        }

        public void StartSearch()
        {
            if (this.IsYoutube)
            {
                Task.Factory.StartNew(() => this.OnPropertyChanged(vm => vm.SelectableYoutubeSongs));
            }

            else
            {
                Task.Factory.StartNew(() => this.OnPropertyChanged(vm => vm.SelectableLocalSongs));
            }
        }

        private void LibraryRaisedSongFinished(object sender, EventArgs e)
        {
            // We need this check, to avoid that the pause/play button changes its state,
            // when the library starts the next song
            if (!this.library.CanPlayNextSong)
            {
                this.OnPropertyChanged(vm => vm.IsPlaying);
            }

            this.OnPropertyChanged(vm => vm.Playlist);

            this.updateTimer.Stop();
        }

        private void LibraryRaisedSongStarted(object sender, EventArgs e)
        {
            this.UpdateTotalTime();

            this.OnPropertyChanged(vm => vm.IsPlaying);
            this.OnPropertyChanged(vm => vm.Playlist);

            this.OnPropertyChanged(vm => vm.SongsRemaining);
            this.OnPropertyChanged(vm => vm.TimeRemaining);

            this.updateTimer.Start();
        }

        private void UpdateCurrentTime()
        {
            this.OnPropertyChanged(vm => vm.CurrentSeconds);
            this.OnPropertyChanged(vm => vm.CurrentTime);
        }

        private void UpdateRemainingPlaylistTimeout()
        {
            if (this.RemainingPlaylistTimeout > TimeSpan.Zero)
            {
                this.OnPropertyChanged(vm => vm.RemainingPlaylistTimeout);
            }
        }

        private void UpdateTotalTime()
        {
            this.OnPropertyChanged(vm => vm.TotalSeconds);
            this.OnPropertyChanged(vm => vm.TotalTime);
        }

        private void UpdateUserAccess()
        {
            this.OnPropertyChanged(vm => vm.IsAdmin);
            this.OnPropertyChanged(vm => vm.CanChangeVolume);
            this.OnPropertyChanged(vm => vm.CanChangeTime);
        }
    }
}