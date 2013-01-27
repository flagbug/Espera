using Caliburn.Micro;
using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using Rareform.Extensions;
using Rareform.Patterns.MVVM;
using ReactiveUI;
using ReactiveUI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Timers;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class ShellViewModel : ReactiveObject, IDisposable
    {
        private readonly ObservableAsPropertyHelper<bool> canModifyWindow;
        private readonly ObservableAsPropertyHelper<ISongSourceViewModel> currentSongSource;
        private readonly ObservableAsPropertyHelper<bool> isAdmin;
        private readonly Library library;
        private readonly ReactiveCollection<PlaylistViewModel> playlists;
        private readonly Timer playlistTimeoutUpdateTimer;
        private readonly Timer updateTimer;
        private bool displayTimeoutWarning;
        private bool isLocal;
        private bool isYoutube;
        private IEnumerable<PlaylistEntryViewModel> selectedPlaylistEntries;

        public ShellViewModel(Library library, IWindowManager windowManager)
        {
            this.library = library;

            this.library.Initialize();

            this.library.SongStarted += (sender, args) => this.HandleSongStarted();
            this.library.SongFinished += (sender, args) => this.HandleSongFinished();
            this.library.SongCorrupted += (sender, args) => this.HandleSongCorrupted();
            this.library.AccessMode.Subscribe(x => this.UpdateUserAccess());
            this.UpdateScreenState = this.library.AccessMode;
            this.library.PlaylistChanged += (sender, e) => this.UpdatePlaylist();

            if (!this.library.Playlists.Any())
            {
                this.library.AddAndSwitchToPlaylist(this.GetNewPlaylistName());
            }

            else
            {
                this.library.SwitchToPlaylist(this.library.Playlists.First());
            }

            this.SettingsViewModel = new SettingsViewModel(this.library, windowManager);

            this.LocalViewModel = new LocalViewModel(this.library);
            this.YoutubeViewModel = new YoutubeViewModel(this.library);
            Observable.CombineLatest(this.LocalViewModel.TimeoutWarning, this.YoutubeViewModel.TimeoutWarning)
                .Subscribe(x => this.TriggerTimeoutWarning());

            this.updateTimer = new Timer(333);
            this.updateTimer.Elapsed += (sender, e) => this.UpdateCurrentTime();

            this.playlistTimeoutUpdateTimer = new Timer(333);
            this.playlistTimeoutUpdateTimer.Elapsed += (sender, e) => this.UpdateRemainingPlaylistTimeout();
            this.playlistTimeoutUpdateTimer.Start();

            this.currentSongSource = this.WhenAny(x => x.IsLocal, x => x.IsYoutube,
                (x1, x2) => x1.Value ? (ISongSourceViewModel)this.LocalViewModel : this.YoutubeViewModel)
                .ToProperty(this, x => x.CurrentSongSource, null, ImmediateScheduler.Instance);

            IObservable<bool> isAdminObservable = this.library.AccessMode
                .Select(x => x == AccessMode.Administrator);

            this.isAdmin = isAdminObservable
                .ToProperty(this, x => x.IsAdmin);

            this.MuteCommand = new ReactiveCommand(this.isAdmin);
            this.MuteCommand.Subscribe(x => this.Volume = 0);

            this.UnMuteCommand = new ReactiveCommand(this.isAdmin);
            this.UnMuteCommand.Subscribe(x => this.Volume = 1);

            this.canModifyWindow = isAdminObservable
                .Select(isAdmin => isAdmin || !Settings.Default.LockWindow)
                .ToProperty(this, x => x.CanModifyWindow);

            this.AddPlaylistCommand = new ReactiveCommand(this.WhenAny(x => x.CanSwitchPlaylist, x => x.Value));
            this.AddPlaylistCommand.Subscribe(x => this.AddPlaylist());

            this.playlists = this.library.Playlists.CreateDerivedCollection(this.CreatePlaylistViewModel);

            this.ShowSettingsCommand = new ReactiveCommand();
            this.ShowSettingsCommand.Subscribe(x => this.SettingsViewModel.HandleSettings());

            this.ShufflePlaylistCommand = new ReactiveCommand();
            this.ShufflePlaylistCommand.Subscribe(x =>
            {
                this.library.ShufflePlaylist();

                this.UpdatePlaylist();
            });

            this.PauseContinueCommand = new ReactiveCommand(this
                .WhenAny(x => x.IsPlaying, x => x.Value)
                .Select(x => x ? this.PauseCommand.CanExecute(null) : this.PlayCommand.CanExecute(null)));
            this.PauseContinueCommand.Where(x => this.IsPlaying).Subscribe(x => this.PauseCommand.Execute(null));
            this.PauseContinueCommand.Where(x => !this.IsPlaying).Subscribe(x => this.PlayCommand.Execute(false));

            IObservable<bool> canEditPlaylist = this
                .WhenAny(x => x.CanSwitchPlaylist, x => x.CurrentPlaylist, (x1, x2) => x1.Value && !x2.Value.Model.IsTemporary);
            this.EditPlaylistNameCommand = new ReactiveCommand(canEditPlaylist);
            this.EditPlaylistNameCommand.Subscribe(x => this.CurrentPlaylist.EditName = true);

            this.IsLocal = true;
        }

        public event EventHandler VideoPlayerCallbackChanged
        {
            add { this.library.VideoPlayerCallbackChanged += value; }
            remove { this.library.VideoPlayerCallbackChanged -= value; }
        }

        public IReactiveCommand AddPlaylistCommand { get; private set; }

        public bool CanChangeTime
        {
            get { return this.library.CanChangeTime; }
        }

        public bool CanChangeVolume
        {
            get { return this.library.CanChangeVolume; }
        }

        /// <summary>
        /// Gets a value indicating whether the window can be minimized, maximized or closed
        /// </summary>
        public bool CanModifyWindow
        {
            get { return this.canModifyWindow.Value; }
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
            get { return this.playlists == null ? null : this.playlists.SingleOrDefault(vm => vm.Model == this.library.CurrentPlaylist); }
            set
            {
                if (value != null) // There always has to be a playlist selected
                {
                    this.library.SwitchToPlaylist(value.Model);
                    this.RaisePropertyChanged(x => x.CurrentPlaylist);
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
            get { return this.currentSongSource.Value; }
        }

        public TimeSpan CurrentTime
        {
            get { return this.library.CurrentTime; }
        }

        public bool DisplayTimeoutWarning
        {
            get { return this.displayTimeoutWarning; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public IReactiveCommand EditPlaylistNameCommand { get; private set; }

        public bool IsAdmin
        {
            get { return this.isAdmin.Value; }
        }

        public bool IsLocal
        {
            get { return this.isLocal; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool IsPlaying
        {
            get { return this.library.IsPlaying; }
        }

        public bool IsYoutube
        {
            get { return this.isYoutube; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public LocalViewModel LocalViewModel { get; private set; }

        /// <summary>
        /// Sets the volume to the lowest possible value.
        /// </summary>
        public IReactiveCommand MuteCommand { get; private set; }

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
                        this.RaisePropertyChanged(x => x.IsPlaying);
                    },
                    param => (this.IsAdmin || !this.library.LockPlayPause) && this.IsPlaying
                );
            }
        }

        /// <summary>
        /// A command that decided whether the songs should be paused or continued.
        /// </summary>
        public IReactiveCommand PauseContinueCommand { get; private set; }

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
                            this.RaisePropertyChanged(x => x.IsPlaying);
                        }

                        else
                        {
                            this.library.PlaySong(this.SelectedPlaylistEntries.First().Index);
                        }
                    },
                    param =>

                        // The admin can always play, but if we are in party mode, we have to check whether it is allowed to play
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

        public IReactiveCollection<PlaylistViewModel> Playlists
        {
            get { return this.playlists; }
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
                        int index = this.playlists.IndexOf(this.CurrentPlaylist);

                        this.library.RemovePlaylist(this.CurrentPlaylist.Model);

                        if (!this.library.Playlists.Any())
                        {
                            this.AddPlaylist();
                        }

                        if (this.playlists.Count > index)
                        {
                            this.CurrentPlaylist = this.playlists[index];
                        }

                        else if (this.playlists.Count >= 1)
                        {
                            this.CurrentPlaylist = this.playlists[index - 1];
                        }

                        else
                        {
                            this.CurrentPlaylist = this.playlists[0];
                        }

                        this.RaisePropertyChanged(x => x.Playlists);
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

                        this.RaisePropertyChanged(x => x.CurrentPlaylist);
                        this.RaisePropertyChanged(x => x.SongsRemaining);
                        this.RaisePropertyChanged(x => x.TimeRemaining);
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
                    this.RaisePropertyChanged(x => x.SelectedPlaylistEntries);
                    this.RaisePropertyChanged(x => x.PlayCommand);
                }
            }
        }

        public SettingsViewModel SettingsViewModel { get; private set; }

        public bool ShowPlaylistTimeOut
        {
            get { return this.SettingsViewModel.EnablePlaylistTimeout && !this.IsAdmin; }
        }

        public IReactiveCommand ShowSettingsCommand { get; private set; }

        public IReactiveCommand ShufflePlaylistCommand { get; private set; }

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

        /// <summary>
        /// Sets the volume to the highest possible value.
        /// </summary>
        public IReactiveCommand UnMuteCommand { get; private set; }

        /// <summary>
        /// Occurs when the view should update the screen state to maximized state or restore it to normal state
        /// </summary>
        public IObservable<AccessMode> UpdateScreenState { get; private set; }

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
                this.RaisePropertyChanged(x => x.Volume);
            }
        }

        public YoutubeViewModel YoutubeViewModel { get; private set; }

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

            this.CurrentPlaylist = this.playlists.Last();
            this.CurrentPlaylist.EditName = true;
        }

        private PlaylistViewModel CreatePlaylistViewModel(Playlist playlist)
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
            this.RaisePropertyChanged(x => x.IsPlaying);
            this.RaisePropertyChanged(x => x.CurrentPlaylist);
        }

        private void HandleSongFinished()
        {
            // We need this check, to avoid that the pause/play button changes its state,
            // when the library starts the next song
            if (!this.library.CanPlayNextSong)
            {
                this.RaisePropertyChanged(x => x.IsPlaying);
            }

            this.RaisePropertyChanged(x => x.CurrentPlaylist);

            this.RaisePropertyChanged(x => x.PreviousSongCommand);

            this.updateTimer.Stop();
        }

        private void HandleSongStarted()
        {
            this.UpdateTotalTime();

            this.RaisePropertyChanged(x => x.IsPlaying);
            this.RaisePropertyChanged(x => x.CurrentPlaylist);

            this.RaisePropertyChanged(x => x.SongsRemaining);
            this.RaisePropertyChanged(x => x.TimeRemaining);

            this.RaisePropertyChanged(x => x.PlayCommand);

            this.updateTimer.Start();
        }

        private void TriggerTimeoutWarning()
        {
            this.DisplayTimeoutWarning = true;
            this.DisplayTimeoutWarning = false;
        }

        private void UpdateCurrentTime()
        {
            this.RaisePropertyChanged(x => x.CurrentSeconds);
            this.RaisePropertyChanged(x => x.CurrentTime);
        }

        private void UpdatePlaylist()
        {
            this.RaisePropertyChanged(x => x.CurrentPlaylist);
            this.RaisePropertyChanged(x => x.SongsRemaining);
            this.RaisePropertyChanged(x => x.TimeRemaining);

            if (this.library.EnablePlaylistTimeout)
            {
                this.RaisePropertyChanged(x => x.RemainingPlaylistTimeout);
            }
        }

        private void UpdateRemainingPlaylistTimeout()
        {
            if (this.RemainingPlaylistTimeout > TimeSpan.Zero)
            {
                this.RaisePropertyChanged(x => x.RemainingPlaylistTimeout);
            }
        }

        private void UpdateTotalTime()
        {
            this.RaisePropertyChanged(x => x.TotalSeconds);
            this.RaisePropertyChanged(x => x.TotalTime);
        }

        private void UpdateUserAccess()
        {
            this.RaisePropertyChanged(x => x.CanChangeVolume);
            this.RaisePropertyChanged(x => x.CanChangeTime);
            this.RaisePropertyChanged(x => x.CanSwitchPlaylist);
            this.RaisePropertyChanged(x => x.ShowPlaylistTimeOut);
        }
    }
}