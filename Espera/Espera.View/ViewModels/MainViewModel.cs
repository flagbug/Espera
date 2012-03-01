using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using Espera.Core;
using Espera.Core.Library;
using Rareform.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    internal class MainViewModel : ViewModelBase<MainViewModel>, IDisposable
    {
        private readonly Library library;
        private readonly Timer updateTimer;
        private string selectedArtist;
        private IEnumerable<SongViewModel> selectedSongs;
        private IEnumerable<PlaylistEntryViewModel> selectedPlaylistEntries;
        private string searchText;
        private volatile bool isAdding;
        private bool isLocal;
        private bool isYoutube;

        public AdministratorViewModel AdministratorViewModel { get; private set; }

        public StatusViewModel StatusViewModel { get; private set; }

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

        public bool IsAdmin
        {
            get { return this.library.AccessMode == AccessMode.Administrator; }
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

        public IEnumerable<SongViewModel> SelectableLocalSongs
        {
            get
            {
                // If we are currently adding songs, copy the songs to a new list, so that we don't run into performance issues
                var songs = (this.isAdding ? this.library.Songs.ToList() : this.library.Songs)
                    .AsParallel()
                    .Where(song => song.Artist == this.SelectedArtist);

                return songs.FilterSongs(this.SearchText)
                    .OrderBy(song => song.Album)
                    .ThenBy(song => song.TrackNumber)
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
                    .Select(song => new SongViewModel(song));
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

        public IEnumerable<PlaylistEntryViewModel> Playlist
        {
            get
            {
                var playlist = this.library.Playlist
                    .Select((song, index) => new PlaylistEntryViewModel(song, index))
                    .ToList(); // We want a list, so that ReSharper doesn't complain about multiple enumerations

                if (this.library.CurrentSongPlaylistIndex.HasValue)
                {
                    playlist[this.library.CurrentSongPlaylistIndex.Value].IsPlaying = true;

                    // If there are more than 5 songs from the beginning of the playlist to the current played song,
                    // skip all, but 5 songs to the position of the currently played song
                    if (playlist.TakeWhile(song => !song.IsPlaying).Count() > 5)
                    {
                        playlist = playlist.Skip(this.library.CurrentSongPlaylistIndex.Value - 5).ToList();
                    }

                    foreach (var model in playlist.TakeWhile(song => !song.IsPlaying))
                    {
                        model.IsInactive = true;
                    }
                }

                return playlist;
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

        public TimeSpan TotalTime
        {
            get { return this.library.TotalTime; }
        }

        public int TotalSeconds
        {
            get { return (int)this.TotalTime.TotalSeconds; }
        }

        public TimeSpan CurrentTime
        {
            get { return this.library.CurrentTime; }
        }

        public int CurrentSeconds
        {
            get { return (int)this.CurrentTime.TotalSeconds; }
            set { this.library.CurrentTime = TimeSpan.FromSeconds(value); }
        }

        public bool IsPlaying
        {
            get { return this.library.IsPlaying; }
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

        /// <summary>
        /// Gets the total remaining time of all songs that come after the currently played song.
        /// </summary>
        public TimeSpan TimeRemaining
        {
            get
            {
                var songs = this.Playlist
                    .SkipWhile(song => song.IsInactive).ToList();

                if (songs.Any())
                    return songs.Select(song => song.Duration)
                      .Aggregate((t1, t2) => t1 + t2);

                return TimeSpan.Zero;
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
                        if (this.library.IsPaused)
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
                    param => this.IsAdmin && ((this.SelectedPlaylistEntries != null && this.SelectedPlaylistEntries.Count() == 1) || this.library.LoadedSong != null)
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
                    param => this.IsAdmin && this.SelectedPlaylistEntries != null
                );
            }
        }

        public ICommand AddSelectedSongsToPlaylistCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.library.AddSongsToPlaylist(this.SelectedSongs.Select(song => song.Model));

                        this.OnPropertyChanged(vm => vm.Playlist);
                        this.OnPropertyChanged(vm => vm.SongsRemaining);
                        this.OnPropertyChanged(vm => vm.TimeRemaining);
                    },
                    param => this.SelectedSongs != null && this.SelectedSongs.Any()
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
                    param => this.SelectedSongs != null && this.SelectedSongs.Any()
                );
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel()
        {
            this.library = new Library();
            this.library.SongStarted += LibraryRaisedSongStarted;
            this.library.SongFinished += LibraryRaisedSongFinished;
            this.library.AccessModeChanged += (sender, e) => this.UpdateUserAccess();

            this.AdministratorViewModel = new AdministratorViewModel(this.library);
            this.StatusViewModel = new StatusViewModel();

            this.updateTimer = new Timer(333);
            this.updateTimer.Elapsed += (sender, e) => this.UpdateTime();

            this.searchText = String.Empty;
        }

        public void AddSongs(string folderPath)
        {
            EventHandler<LibraryFillEventArgs> handler =
                (sender, e) => this.StatusViewModel.Update(e.Song.OriginalPath, e.ProcessedTagCount, e.TotalTagCount);

            this.library.SongAdded += handler;

            var artistUpdateTimer = new Timer(5000);

            artistUpdateTimer.Elapsed += (sender, e) => this.OnPropertyChanged(vm => vm.Artists);

            this.isAdding = true;
            this.StatusViewModel.IsAdding = true;
            artistUpdateTimer.Start();

            this.library
                .AddLocalSongsAsync(folderPath)
                .ContinueWith(task =>
                {
                    this.library.SongAdded -= handler;

                    artistUpdateTimer.Stop();
                    artistUpdateTimer.Dispose();

                    this.OnPropertyChanged(vm => vm.Artists);
                    this.isAdding = false;
                    this.StatusViewModel.Reset();
                });
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.library.Dispose();
            this.updateTimer.Dispose();
        }

        private void UpdateTime()
        {
            this.OnPropertyChanged(vm => vm.CurrentSeconds);
            this.OnPropertyChanged(vm => vm.CurrentTime);
        }

        private void LibraryRaisedSongFinished(object sender, EventArgs e)
        {
            this.OnPropertyChanged(vm => vm.IsPlaying);
            this.OnPropertyChanged(vm => vm.Playlist);

            this.updateTimer.Stop();
        }

        private void LibraryRaisedSongStarted(object sender, EventArgs e)
        {
            this.OnPropertyChanged(vm => vm.TotalSeconds);
            this.OnPropertyChanged(vm => vm.TotalTime);
            this.OnPropertyChanged(vm => vm.Playlist);
            this.OnPropertyChanged(vm => vm.SongsRemaining);
            this.OnPropertyChanged(vm => vm.TimeRemaining);
            this.OnPropertyChanged(vm => vm.IsPlaying);

            this.updateTimer.Start();
        }

        private void UpdateUserAccess()
        {
            this.OnPropertyChanged(vm => vm.IsAdmin);
        }
    }
}