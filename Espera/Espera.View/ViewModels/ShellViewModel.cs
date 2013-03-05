using Caliburn.Micro;
using Espera.Core;
using Espera.Core.Audio;
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
using System.Windows;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class ShellViewModel : ReactiveObject, IDisposable
    {
        private readonly ObservableAsPropertyHelper<bool> canChangeTime;
        private readonly ObservableAsPropertyHelper<bool> canChangeVolume;
        private readonly ObservableAsPropertyHelper<bool> canModifyWindow;
        private readonly ObservableAsPropertyHelper<bool> canSwitchPlaylist;
        private readonly ObservableAsPropertyHelper<ISongSourceViewModel> currentSongSource;
        private readonly ObservableAsPropertyHelper<bool> isAdmin;
        private readonly ObservableAsPropertyHelper<bool> isPlaying;
        private readonly Library library;
        private readonly ReactiveCollection<PlaylistViewModel> playlists;
        private readonly Timer playlistTimeoutUpdateTimer;
        private readonly ObservableAsPropertyHelper<bool> showPlaylistTimeout;
        private readonly ObservableAsPropertyHelper<TimeSpan> totalTime;
        private readonly Timer updateTimer;
        private bool displayTimeoutWarning;
        private bool isLocal;
        private bool isYoutube;
        private IEnumerable<PlaylistEntryViewModel> selectedPlaylistEntries;

        public ShellViewModel(Library library, IWindowManager windowManager)
        {
            this.library = library;

            this.library.Initialize();

            this.library.SongStarted.Subscribe(x => this.HandleSongStarted());
            this.library.SongFinished.Subscribe(x => this.HandleSongFinished());
            this.UpdateScreenState = this.library.AccessMode;

            this.canChangeTime = this.library.CanChangeTime.ToProperty(this, x => x.CanChangeTime);
            this.canChangeVolume = this.library.CanChangeVolume.ToProperty(this, x => x.CanChangeVolume);
            this.canSwitchPlaylist = this.library.CanSwitchPlaylist.ToProperty(this, x => x.CanSwitchPlaylist);

            IObservable<bool> isAdminObservable = this.library.AccessMode
                .Select(x => x == AccessMode.Administrator);

            this.NextSongCommand = new ReactiveCommand(isAdminObservable
                .CombineLatest(this.library.CanPlayNextSong, (b, b1) => b && b1));
            this.NextSongCommand.Subscribe(x => this.library.PlayNextSong());

            this.PreviousSongCommand = new ReactiveCommand(isAdminObservable
                .CombineLatest(this.library.CanPlayPreviousSong, (b, b1) => b && b1));
            this.PreviousSongCommand.Subscribe(x => this.library.PlayPreviousSong());

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

            this.isAdmin = isAdminObservable
                .ToProperty(this, x => x.IsAdmin, true);

            this.showPlaylistTimeout = isAdminObservable
                .CombineLatest(this.WhenAny(x => x.SettingsViewModel.EnablePlaylistTimeout, x => x.Value), (isAdmin, enableTimeout) => !isAdmin && enableTimeout)
                .ToProperty(this, x => x.ShowPlaylistTimeout);

            this.MuteCommand = new ReactiveCommand(this.isAdmin);
            this.MuteCommand.Subscribe(x => this.Volume = 0);

            this.UnMuteCommand = new ReactiveCommand(this.isAdmin);
            this.UnMuteCommand.Subscribe(x => this.Volume = 1);

            this.canModifyWindow = isAdminObservable
                .Select(isAdmin => isAdmin || !Settings.Default.LockWindow)
                .ToProperty(this, x => x.CanModifyWindow);

            this.isPlaying = this.library.PlaybackState
                .Select(x => x == AudioPlayerState.Playing)
                .ToProperty(this, x => x.IsPlaying);

            this.totalTime = this.library.TotalTime
                .ToProperty(this, x => x.TotalTime);

            this.AddPlaylistCommand = new ReactiveCommand(this.WhenAny(x => x.CanSwitchPlaylist, x => x.Value));
            this.AddPlaylistCommand.Subscribe(x => this.AddPlaylist());

            this.playlists = this.library.Playlists.CreateDerivedCollection(this.CreatePlaylistViewModel);
            this.playlists.ItemsRemoved.Subscribe(x => x.Dispose());

            this.ShowSettingsCommand = new ReactiveCommand();
            this.ShowSettingsCommand.Subscribe(x => this.SettingsViewModel.HandleSettings());

            this.ShufflePlaylistCommand = new ReactiveCommand();
            this.ShufflePlaylistCommand.Subscribe(x => this.library.ShufflePlaylist());

            this.PauseContinueCommand = new ReactiveCommand(this
                .WhenAny(x => x.IsPlaying, x => x.Value)
                .Select(x => x ? this.PauseCommand.CanExecute(null) : this.PlayCommand.CanExecute(null)));
            this.PauseContinueCommand.Subscribe(x =>
            {
                if (this.IsPlaying)
                {
                    this.PauseCommand.Execute(null);
                }

                else
                {
                    this.PlayCommand.Execute(false);
                }
            });

            IObservable<bool> canEditPlaylist = this
                .WhenAny(x => x.CanSwitchPlaylist, x => x.CurrentPlaylist, (x1, x2) => x1.Value && !x2.Value.Model.IsTemporary);
            this.EditPlaylistNameCommand = new ReactiveCommand(canEditPlaylist);
            this.EditPlaylistNameCommand.Subscribe(x => this.CurrentPlaylist.EditName = true);

            this.RemovePlaylistCommand = new ReactiveCommand(this.WhenAny(x => x.CurrentEditedPlaylist, x => x.CurrentPlaylist, (x1, x2) => x1 != null || x2 != null));
            this.RemovePlaylistCommand.Subscribe(x =>
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
            });

            this.PauseCommand = new ReactiveCommand(this.isAdmin.CombineLatest(this.library.LockPlayPause, this.isPlaying,
                (isAdmin, lockPlayPause, isPlaying) => (isAdmin || !lockPlayPause) && isPlaying));
            this.PauseCommand.Subscribe(x =>
            {
                this.library.PauseSong();
                this.updateTimer.Stop();
            });

            this.IsLocal = true;
        }

        public IReactiveCommand AddPlaylistCommand { get; private set; }

        public bool CanChangeTime
        {
            get { return this.canChangeTime.Value; }
        }

        public bool CanChangeVolume
        {
            get { return this.canChangeVolume.Value; }
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
            get { return this.canSwitchPlaylist.Value; }
        }

        public PlaylistViewModel CurrentEditedPlaylist
        {
            get { return this.Playlists.SingleOrDefault(playlist => playlist.EditName); }
        }

        public PlaylistViewModel CurrentPlaylist
        {
            get { return this.playlists.SingleOrDefault(vm => vm.Model == this.library.CurrentPlaylist); }
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
            get { return this.isPlaying.Value; }
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
        public IReactiveCommand NextSongCommand { get; private set; }

        /// <summary>
        /// Pauses the currently played song.
        /// </summary>
        public IReactiveCommand PauseCommand { get; private set; }

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
                        if (this.library.PlaybackState.FirstAsync().Wait() == AudioPlayerState.Paused || this.library.LoadedSong.FirstAsync().Wait() != null)
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
                        (this.IsAdmin || !this.library.LockPlayPause.Value) &&

                        // If exactly one song is selected, the command can be executed
                        (this.SelectedPlaylistEntries != null && this.SelectedPlaylistEntries.Count() == 1 ||

                        // If the current song is paused, the command can be executed
                        (this.library.LoadedSong.FirstAsync().Wait() != null || this.library.PlaybackState.FirstAsync().Wait() == AudioPlayerState.Paused))
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

        public GridLength PlaylistHeight
        {
            get { return (GridLength)new GridLengthConverter().ConvertFromString(Settings.Default.PlaylistHeight); }
            set
            {
                Settings.Default.PlaylistHeight = new GridLengthConverter().ConvertToString(value);
            }
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
                    param => (this.IsAdmin || !this.library.LockPlayPause.Value) && (this.SelectedPlaylistEntries != null && this.SelectedPlaylistEntries.Count() == 1)
               );
            }
        }

        /// <summary>
        /// Plays the song that is before the currently played song in the playlist.
        /// </summary>
        public IReactiveCommand PreviousSongCommand { get; private set; }

        public TimeSpan RemainingPlaylistTimeout
        {
            get { return this.library.RemainingPlaylistTimeout; }
        }

        public IReactiveCommand RemovePlaylistCommand { get; private set; }

        public ICommand RemoveSelectedPlaylistEntriesCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.library.RemoveFromPlaylist(this.SelectedPlaylistEntries.Select(entry => entry.Index)),
                    param => this.SelectedPlaylistEntries != null
                        && this.SelectedPlaylistEntries.Any()
                        && (this.IsAdmin || !this.library.LockPlaylistRemoval.Value)
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

        public bool ShowPlaylistTimeout
        {
            get { return this.showPlaylistTimeout.Value; }
        }

        public IReactiveCommand ShowSettingsCommand { get; private set; }

        public IReactiveCommand ShufflePlaylistCommand { get; private set; }

        public GridLength SongSourceHeight
        {
            get { return (GridLength)new GridLengthConverter().ConvertFromString(Settings.Default.SongSourceHeight); }
            set
            {
                Settings.Default.SongSourceHeight = new GridLengthConverter().ConvertToString(value);
            }
        }

        public int TotalSeconds
        {
            get { return (int)this.TotalTime.TotalSeconds; }
        }

        public TimeSpan TotalTime
        {
            get { return this.totalTime.Value; }
        }

        /// <summary>
        /// Sets the volume to the highest possible value.
        /// </summary>
        public IReactiveCommand UnMuteCommand { get; private set; }

        /// <summary>
        /// Occurs when the view should update the screen state to maximized state or restore it to normal state
        /// </summary>
        public IObservable<AccessMode> UpdateScreenState { get; private set; }

        public IObservable<IVideoPlayerCallback> VideoPlayerCallback
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

        private void HandleSongFinished()
        {
            this.updateTimer.Stop();
        }

        private void HandleSongStarted()
        {
            this.UpdateTotalTime();

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
    }
}