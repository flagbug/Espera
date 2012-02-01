using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Windows.Input;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View
{
    internal class MainViewModel : ViewModelBase<MainViewModel>, IDisposable
    {
        private readonly Library library;
        private readonly Timer updateTimer;
        private string selectedArtist;
        private bool isAdding;
        private string currentAddingPath;
        private SongViewModel selectedSong;
        private int selectedPlaylistIndex;
        private string searchText;
        private bool showAdministratorPanel;

        public string SearchText
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

        public IEnumerable<string> Artists
        {
            get
            {
                // If we are currently adding songs, copy the songs to a new list, so that we don't run into performance issues
                IEnumerable<Song> songs = this.IsAdding ? this.library.Songs.ToList() : this.library.Songs;

                return SearchEngine.FilterSongs(songs, this.SearchText)
                    .GroupBy(song => song.Artist)
                    .Select(group => group.Key)
                    .OrderBy(artist => artist)
                    .Where(artist => songs.Any(song => song.Artist == artist));
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

        public IEnumerable<SongViewModel> SelectableSongs
        {
            get
            {
                // If we are currently adding songs, copy the songs to a new list, so that we don't run into performance issues
                var songs = (this.IsAdding ? this.library.Songs.ToList() : this.library.Songs)
                    .AsParallel()
                    .Where(song => song.Artist == this.SelectedArtist);

                return SearchEngine.FilterSongs(songs, this.SearchText)
                    .OrderBy(song => song.Album)
                    .ThenBy(song => song.TrackNumber)
                    .Select(song => new SongViewModel(song));
            }
        }

        public SongViewModel SelectedSong
        {
            get { return this.selectedSong; }
            set
            {
                if (this.SelectedSong != value)
                {
                    this.selectedSong = value;
                    this.OnPropertyChanged(vm => vm.SelectedSong);
                }
            }
        }

        public int SelectedPlaylistIndex
        {
            get { return this.selectedPlaylistIndex; }
            set
            {
                if (this.SelectedPlaylistIndex != value)
                {
                    this.selectedPlaylistIndex = value;
                    this.OnPropertyChanged(vm => vm.SelectedPlaylistIndex);
                    this.OnPropertyChanged(vm => vm.PlayCommand);
                }
            }
        }

        public IEnumerable<SongViewModel> Playlist
        {
            get
            {
                var playlist = this.library
                    .Playlist
                    .Select(song => new SongViewModel(song))
                    .ToList();

                if (this.library.CurrentSongPlaylistIndex.HasValue)
                {
                    playlist[this.library.CurrentSongPlaylistIndex.Value].IsPlaying = true;
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

        public bool IsAdding
        {
            get { return this.isAdding; }
            private set
            {
                if (this.IsAdding != value)
                {
                    this.isAdding = value;
                    this.OnPropertyChanged(vm => vm.IsAdding);
                }
            }
        }

        public string CurrentAddingPath
        {
            get { return this.currentAddingPath; }
            private set
            {
                if (this.CurrentAddingPath != value)
                {
                    this.currentAddingPath = value;
                    this.OnPropertyChanged(vm => vm.CurrentAddingPath);
                }
            }
        }

        public bool IsAdministrator
        {
            get { return this.library.AccessMode == AccessMode.Administrator; }
        }

        public bool ShowAdministratorPanel
        {
            get { return this.showAdministratorPanel; }
            set
            {
                this.showAdministratorPanel = value;

                this.OnPropertyChanged(vm => vm.ShowAdministratorPanel);
            }
        }

        public ICommand ToggleAdministratorPanel
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.ShowAdministratorPanel = !this.ShowAdministratorPanel;
                    }
                );
            }
        }

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
                            this.OnPropertyChanged(vm => vm.IsPlaying);
                        }

                        else
                        {
                            this.library.PlaySong(this.SelectedPlaylistIndex);
                        }

                        this.updateTimer.Start();
                    },
                    param => this.SelectedPlaylistIndex != -1 || this.library.LoadedSong != null
                );
            }
        }

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
                    }
                );
            }
        }

        public ICommand NextSongCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.library.PlayNextSong(),
                    param => this.library.CanPlayNextSong
                );
            }
        }

        public ICommand PreviousSongCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.library.PlayPreviousSong(),
                    param => this.library.CanPlayPreviousSong
                );
            }
        }

        public ICommand MuteCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.Volume = 0
                );
            }
        }

        public ICommand UnMuteCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.Volume = 1
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
            this.updateTimer = new Timer(333);
            this.updateTimer.Elapsed += UpdateTimerElapsed;
            this.searchText = String.Empty;
            this.SelectedPlaylistIndex = -1;
        }

        public void AddSelectedSongToPlaylist()
        {
            this.library.AddSongToPlaylist(this.SelectedSong.Model);
            this.OnPropertyChanged(vm => vm.Playlist);
        }

        public void AddSongs(string folderPath)
        {
            EventHandler<SongEventArgs> handler = (sender, e) =>
            {
                this.CurrentAddingPath = e.Song.Path.LocalPath;
            };

            this.library.SongAdded += handler;

            var artistUpdateTimer = new Timer(5000);

            artistUpdateTimer.Elapsed += (sender, e) => this.OnPropertyChanged(vm => vm.Artists);

            this.IsAdding = true;
            artistUpdateTimer.Start();

            this.library
                .AddLocalSongsAsync(folderPath)
                .ContinueWith(task =>
                {
                    this.library.SongAdded -= handler;

                    artistUpdateTimer.Stop();
                    artistUpdateTimer.Dispose();

                    this.OnPropertyChanged(vm => vm.Artists);
                    this.IsAdding = false;
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

        private void UpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            this.OnPropertyChanged(vm => vm.CurrentSeconds);
            this.OnPropertyChanged(vm => vm.CurrentTime);
        }

        private void LibraryRaisedSongFinished(object sender, EventArgs e)
        {
            this.OnPropertyChanged(vm => vm.IsPlaying);
        }

        private void LibraryRaisedSongStarted(object sender, EventArgs e)
        {
            this.OnPropertyChanged(vm => vm.TotalSeconds);
            this.OnPropertyChanged(vm => vm.TotalTime);
            this.OnPropertyChanged(vm => vm.Playlist);
            this.OnPropertyChanged(vm => vm.IsPlaying);
        }
    }
}