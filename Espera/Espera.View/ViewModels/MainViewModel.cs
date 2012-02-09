using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Windows.Input;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View.ViewModels
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
        private int processedTags;
        private int totalTags;

        public AdministratorViewModel AdministratorViewModel { get; private set; }

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

        public int ProcessedTags
        {
            get { return this.processedTags; }
            private set
            {
                if (this.ProcessedTags != value)
                {
                    this.processedTags = value;
                    this.OnPropertyChanged(vm => vm.ProcessedTags);
                }
            }
        }

        public int TotalTags
        {
            get { return this.totalTags; }
            private set
            {
                if (this.totalTags != value)
                {
                    this.totalTags = value;
                    this.OnPropertyChanged(vm => vm.TotalTags);
                }
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
                    param => this.IsAdmin && (this.SelectedPlaylistIndex != -1 || this.library.LoadedSong != null)
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
                    },
                    param => this.IsAdmin
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
                    param => this.IsAdmin && this.library.CanPlayNextSong
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
                    param => this.IsAdmin && this.library.CanPlayPreviousSong
                );
            }
        }

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

            this.updateTimer = new Timer(333);
            this.updateTimer.Elapsed += (sender, e) => this.UpdateTime();

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
            EventHandler<LibraryFillEventArgs> handler = (sender, e) =>
            {
                this.CurrentAddingPath = e.Song.Path.LocalPath;
                this.TotalTags = e.TotalTagCount;
                this.ProcessedTags = e.ProcessedTagCount;
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

        private void UpdateTime()
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

        private void UpdateUserAccess()
        {
            this.OnPropertyChanged(vm => vm.IsAdmin);
        }
    }
}