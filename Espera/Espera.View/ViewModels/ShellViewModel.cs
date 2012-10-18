using Caliburn.Micro;
using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using Rareform.Extensions;
using Rareform.Patterns.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal sealed class ShellViewModel : PropertyChangedBase, IDisposable
    {
        private readonly Library library;
        private readonly Timer playlistTimeoutUpdateTimer;
        private readonly Timer updateTimer;
        private ISongSourceViewModel currentSongSource;
        private bool displayTimeoutWarning;
        private bool isLocal;
        private bool isYoutube;
        private ObservableCollection<PlaylistViewModel> playlists;
        private IEnumerable<PlaylistEntryViewModel> selectedPlaylistEntries;

        public ShellViewModel()
        {
            this.library = new Library(new RemovableDriveWatcher());
            this.library.Initialize();

            this.library.SongStarted += (sender, args) => this.HandleSongStarted();
            this.library.SongFinished += (sender, args) => this.HandleSongFinished();
            this.library.SongCorrupted += (sender, args) => this.HandleSongCorrupted();
            this.library.AccessModeChanged += (sender, e) => this.UpdateUserAccess();
            this.library.PlaylistChanged += (sender, e) => this.UpdatePlaylist();

            if (!this.library.Playlists.Any())
            {
                this.library.AddAndSwitchToPlaylist(this.GetNewPlaylistName());
            }

            else
            {
                this.library.SwitchToPlaylist(this.library.Playlists.First().Name);
            }

            this.AdministratorViewModel = new AdministratorViewModel(this.library);

            this.LocalViewModel = new LocalViewModel(this.library);
            this.LocalViewModel.TimeoutWarning += (sender, e) => this.TriggerTimeoutWarning();

            this.YoutubeViewModel = new YoutubeViewModel(this.library);
            this.YoutubeViewModel.TimeoutWarning += (sender, e) => this.TriggerTimeoutWarning();

            this.updateTimer = new Timer(333);
            this.updateTimer.Elapsed += (sender, e) => this.UpdateCurrentTime();

            this.playlistTimeoutUpdateTimer = new Timer(333);
            this.playlistTimeoutUpdateTimer.Elapsed += (sender, e) => this.UpdateRemainingPlaylistTimeout();
            this.playlistTimeoutUpdateTimer.Start();
        }

        public event EventHandler VideoPlayerCallbackChanged
        {
            add { this.library.VideoPlayerCallbackChanged += value; }
            remove { this.library.VideoPlayerCallbackChanged -= value; }
        }

        public ICommand AddPlaylistCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.AddPlaylist()
                );
            }
        }

        public AdministratorViewModel AdministratorViewModel { get; private set; }

        public bool CanChangeTime
        {
            get { return this.library.CanChangeTime; }
        }

        public bool CanChangeVolume
        {
            get { return this.library.CanChangeVolume; }
        }

        public bool CanSwitchPlaylist
        {
            get { return this.library.CanSwitchPlaylist; }
        }

        public PlaylistViewModel CurrentEditedPlaylist
        {
            get { return this.Playlists.SingleOrDefault(playlist => playlist.EditName); }
        }

        public PlaylistViewModel CurrentPlaylist
        {
            get { return this.playlists == null ? null : this.playlists.Single(vm => vm.Name == this.library.CurrentPlaylist.Name); }
            set
            {
                if (value != null) // There always has to be a playlist selected
                {
                    this.library.SwitchToPlaylist(value.Name);
                    this.NotifyOfPropertyChange(() => this.CurrentPlaylist);
                }
            }
        }

        public int CurrentSeconds
        {
            get { return (int)this.CurrentTime.TotalSeconds; }
            set { this.library.CurrentTime = TimeSpan.FromSeconds(value); }
        }

        public ISongSourceViewModel CurrentSongSource
        {
            get { return this.currentSongSource; }
            private set
            {
                if (this.CurrentSongSource != value)
                {
                    this.currentSongSource = value;
                    this.NotifyOfPropertyChange(() => this.CurrentSongSource);
                }
            }
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
                    this.NotifyOfPropertyChange(() => this.DisplayTimeoutWarning);
                }
            }
        }

        public ICommand EditPlaylistNameCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.CurrentPlaylist.EditName = true;
                    }
                );
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
                    this.NotifyOfPropertyChange(() => this.IsLocal);

                    if (this.IsLocal)
                    {
                        this.CurrentSongSource = this.LocalViewModel;
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
                    this.NotifyOfPropertyChange(() => this.IsYoutube);

                    if (this.IsYoutube)
                    {
                        this.CurrentSongSource = this.YoutubeViewModel;
                    }
                }
            }
        }

        public LocalViewModel LocalViewModel { get; private set; }

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
                        this.NotifyOfPropertyChange(() => this.IsPlaying);
                    },
                    param => (this.IsAdmin || !this.library.LockPlayPause) && this.IsPlaying
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
                        if (this.library.IsPaused || this.library.LoadedSong != null)
                        {
                            this.library.ContinueSong();
                            this.updateTimer.Start();
                            this.NotifyOfPropertyChange(() => this.IsPlaying);
                        }

                        else
                        {
                            this.library.PlaySong(this.SelectedPlaylistEntries.First().Index);
                        }
                    },
                    param =>

                        // The admin can always play, but if we are in party mode, we have to check wheter it is allowed to play
                        (this.IsAdmin || !this.library.LockPlayPause) &&

                        // If exactly one song is selected, the command can be executed
                        (this.SelectedPlaylistEntries != null && this.SelectedPlaylistEntries.Count() == 1 ||

                        // If the current song is paused, the command can be executed
                        (this.library.LoadedSong != null || this.library.IsPaused))
                );
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

        public ObservableCollection<PlaylistViewModel> Playlists
        {
            get
            {
                var vms = this.library.Playlists.Select(this.CreatePlaylistViewModel);

                return this.playlists ??
                    (this.playlists = new ObservableCollection<PlaylistViewModel>(vms));
            }
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

        public ICommand PlayOverrideCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.library.PlaySong(this.SelectedPlaylistEntries.First().Index),
                    param => (this.IsAdmin || !this.library.LockPlayPause) && (this.SelectedPlaylistEntries != null && this.SelectedPlaylistEntries.Count() == 1)
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

        public TimeSpan RemainingPlaylistTimeout
        {
            get { return this.library.RemainingPlaylistTimeout; }
        }

        public ICommand RemovePlaylistCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        int index = this.Playlists.IndexOf(this.CurrentPlaylist);

                        this.library.RemovePlaylist(this.CurrentPlaylist.Name);

                        this.Playlists.Remove(this.CurrentPlaylist);

                        if (!this.library.Playlists.Any())
                        {
                            this.AddPlaylist();
                        }

                        if (this.Playlists.Count > index)
                        {
                            this.CurrentPlaylist = this.Playlists[index];
                        }

                        else if (this.Playlists.Count >= 1)
                        {
                            this.CurrentPlaylist = this.Playlists[index - 1];
                        }

                        else
                        {
                            this.CurrentPlaylist = this.Playlists[0];
                        }

                        this.NotifyOfPropertyChange(() => this.Playlists);
                    },
                    param => this.CurrentEditedPlaylist != null || this.CurrentPlaylist != null
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

                        this.NotifyOfPropertyChange(() => this.CurrentPlaylist);
                        this.NotifyOfPropertyChange(() => this.SongsRemaining);
                        this.NotifyOfPropertyChange(() => this.TimeRemaining);
                    },
                    param => this.SelectedPlaylistEntries != null
                        && this.SelectedPlaylistEntries.Any()
                        && (this.IsAdmin || !this.library.LockPlaylistRemoval)
                );
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
                    this.NotifyOfPropertyChange(() => this.SelectedPlaylistEntries);
                    this.NotifyOfPropertyChange(() => this.PlayCommand);
                }
            }
        }

        public bool ShowPlaylistTimeOut
        {
            get { return this.AdministratorViewModel.EnablePlaylistTimeout && !this.IsAdmin; }
        }

        public ICommand ShufflePlaylistCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.library.ShufflePlaylist();

                        this.UpdatePlaylist();
                    }
                );
            }
        }

        /// <summary>
        /// Gets the number of songs that come after the currently played song.
        /// </summary>
        public int SongsRemaining
        {
            get
            {
                return this.CurrentPlaylist.Songs
                    .SkipWhile(song => song.IsInactive)
                    .Count();
            }
        }

        /// <summary>
        /// Gets the total remaining time of all songs that come after the currently played song.
        /// </summary>
        public TimeSpan? TimeRemaining
        {
            get
            {
                var songs = this.CurrentPlaylist.Songs
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

        public IVideoPlayerCallback VideoPlayerCallback
        {
            get { return this.library.VideoPlayerCallback; }
        }

        public double Volume
        {
            get { return this.library.Volume; }
            set
            {
                this.library.Volume = (float)value;
                this.NotifyOfPropertyChange(() => this.Volume);
            }
        }

        public YoutubeViewModel YoutubeViewModel { get; private set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Settings.Default.Save();

            this.library.Save();
            this.library.Dispose();

            this.playlistTimeoutUpdateTimer.Dispose();
            this.updateTimer.Dispose();
        }

        private void AddPlaylist()
        {
            this.library.AddAndSwitchToPlaylist(this.GetNewPlaylistName());

            PlaylistViewModel newPlaylist = this.CreatePlaylistViewModel(this.library.Playlists.Last());
            this.Playlists.Add(newPlaylist);

            this.CurrentPlaylist = newPlaylist;
            this.CurrentPlaylist.EditName = true;
        }

        private PlaylistViewModel CreatePlaylistViewModel(PlaylistInfo playlist)
        {
            return new PlaylistViewModel(playlist, name => this.playlists.Count(p => p.Name == name) == 1);
        }

        private string GetNewPlaylistName()
        {
            string newName = (this.playlists ?? Enumerable.Empty<PlaylistViewModel>())
                .Select(playlist => playlist.Name)
                .CreateUnique(i =>
                    {
                        string name = "New Playlist";

                        if (i > 1)
                        {
                            name += " " + i;
                        }

                        return name;
                    });

            return newName;
        }

        private void HandleSongCorrupted()
        {
            this.NotifyOfPropertyChange(() => this.IsPlaying);
            this.NotifyOfPropertyChange(() => this.CurrentPlaylist);
        }

        private void HandleSongFinished()
        {
            // We need this check, to avoid that the pause/play button changes its state,
            // when the library starts the next song
            if (!this.library.CanPlayNextSong)
            {
                this.NotifyOfPropertyChange(() => this.IsPlaying);
            }

            this.NotifyOfPropertyChange(() => this.CurrentPlaylist);

            this.NotifyOfPropertyChange(() => this.PreviousSongCommand);

            this.updateTimer.Stop();
        }

        private void HandleSongStarted()
        {
            this.UpdateTotalTime();

            this.NotifyOfPropertyChange(() => this.IsPlaying);
            this.NotifyOfPropertyChange(() => this.CurrentPlaylist);

            this.NotifyOfPropertyChange(() => this.SongsRemaining);
            this.NotifyOfPropertyChange(() => this.TimeRemaining);

            this.NotifyOfPropertyChange(() => this.PlayCommand);

            this.updateTimer.Start();
        }

        private void TriggerTimeoutWarning()
        {
            this.DisplayTimeoutWarning = true;
            this.DisplayTimeoutWarning = false;
        }

        private void UpdateCurrentTime()
        {
            this.NotifyOfPropertyChange(() => this.CurrentSeconds);
            this.NotifyOfPropertyChange(() => this.CurrentTime);
        }

        private void UpdatePlaylist()
        {
            this.NotifyOfPropertyChange(() => this.CurrentPlaylist);
            this.NotifyOfPropertyChange(() => this.SongsRemaining);
            this.NotifyOfPropertyChange(() => this.TimeRemaining);

            if (this.library.EnablePlaylistTimeout)
            {
                this.NotifyOfPropertyChange(() => this.RemainingPlaylistTimeout);
            }
        }

        private void UpdateRemainingPlaylistTimeout()
        {
            if (this.RemainingPlaylistTimeout > TimeSpan.Zero)
            {
                this.NotifyOfPropertyChange(() => this.RemainingPlaylistTimeout);
            }
        }

        private void UpdateTotalTime()
        {
            this.NotifyOfPropertyChange(() => this.TotalSeconds);
            this.NotifyOfPropertyChange(() => this.TotalTime);
        }

        private void UpdateUserAccess()
        {
            this.NotifyOfPropertyChange(() => this.IsAdmin);
            this.NotifyOfPropertyChange(() => this.CanChangeVolume);
            this.NotifyOfPropertyChange(() => this.CanChangeTime);
            this.NotifyOfPropertyChange(() => this.CanSwitchPlaylist);
            this.NotifyOfPropertyChange(() => this.ShowPlaylistTimeOut);
        }
    }
}