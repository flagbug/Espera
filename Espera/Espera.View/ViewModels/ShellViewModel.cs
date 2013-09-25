﻿using Caliburn.Micro;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.View.Properties;
using Rareform.Extensions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Timers;
using System.Windows;

namespace Espera.View.ViewModels
{
    internal class ShellViewModel : ReactiveObject, IDisposable
    {
        private readonly ObservableAsPropertyHelper<bool> canChangeTime;
        private readonly ObservableAsPropertyHelper<bool> canChangeVolume;
        private readonly ObservableAsPropertyHelper<bool> canModifyWindow;
        private readonly ObservableAsPropertyHelper<bool> canSwitchPlaylist;
        private readonly ObservableAsPropertyHelper<int> currentSeconds;
        private readonly ObservableAsPropertyHelper<ISongSourceViewModel> currentSongSource;
        private readonly ObservableAsPropertyHelper<TimeSpan> currentTime;
        private readonly ObservableAsPropertyHelper<bool> isAdmin;
        private readonly ObservableAsPropertyHelper<bool> isPlaying;
        private readonly Library library;
        private readonly Timer playlistTimeoutUpdateTimer;
        private readonly ObservableAsPropertyHelper<bool> showPlaylistTimeout;
        private readonly ObservableAsPropertyHelper<int> totalSeconds;
        private readonly ObservableAsPropertyHelper<TimeSpan> totalTime;
        private bool displayTimeoutWarning;
        private bool isLocal;
        private bool isYoutube;
        private IEnumerable<PlaylistEntryViewModel> selectedPlaylistEntries;
        private bool showVideoPlayer;

        public ShellViewModel(Library library, IWindowManager windowManager)
        {
            this.library = library;

            this.library.Initialize();

            this.library.CurrentPlaylistChanged.Subscribe(x => this.RaisePropertyChanged("CurrentPlaylist"));
            this.UpdateScreenState = this.library.AccessMode;

            this.canChangeTime = this.library.CanChangeTime.ToProperty(this, x => x.CanChangeTime);
            this.canChangeVolume = this.library.CanChangeVolume.ToProperty(this, x => x.CanChangeVolume);
            this.canSwitchPlaylist = this.library.CanSwitchPlaylist.ToProperty(this, x => x.CanSwitchPlaylist);

            IObservable<bool> isAdminObservable = this.library.AccessMode
                .Select(x => x == AccessMode.Administrator);

            this.NextSongCommand = new ReactiveCommand(isAdminObservable
                .CombineLatest(this.library.CanPlayNextSong, (b, b1) => b && b1));
            this.NextSongCommand.RegisterAsyncTask(_ => this.library.PlayNextSongAsync());

            this.PreviousSongCommand = new ReactiveCommand(isAdminObservable
                .CombineLatest(this.library.CanPlayPreviousSong, (b, b1) => b && b1));
            this.PreviousSongCommand.RegisterAsyncTask(_ => this.library.PlayPreviousSongAsync());

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

            this.playlistTimeoutUpdateTimer = new Timer(333);
            this.playlistTimeoutUpdateTimer.Elapsed += (sender, e) => this.UpdateRemainingPlaylistTimeout();
            this.playlistTimeoutUpdateTimer.Start();

            this.currentSongSource = this.WhenAnyValue(x => x.IsLocal, x => x.IsYoutube,
                (x1, x2) => x1 ? (ISongSourceViewModel)this.LocalViewModel : this.YoutubeViewModel)
                .ToProperty(this, x => x.CurrentSongSource, null, ImmediateScheduler.Instance);

            this.currentSongSource
                .Select(x => x.TimeoutWarning)
                .Switch()
                .Subscribe(_ => this.TriggerTimeoutWarning());

            this.isAdmin = isAdminObservable
                .ToProperty(this, x => x.IsAdmin);

            this.showPlaylistTimeout = isAdminObservable
                .CombineLatest(this.WhenAnyValue(x => x.SettingsViewModel.EnablePlaylistTimeout), (isAdmin, enableTimeout) => !isAdmin && enableTimeout)
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

            this.currentTime = this.library.CurrentTimeChanged
                .ToProperty(this, x => x.CurrentTime);

            this.currentSeconds = this.library.CurrentTimeChanged
                .Select(x => (int)x.TotalSeconds)
                .ToProperty(this, x => x.CurrentSeconds);

            this.totalTime = this.library.TotalTime
                .ToProperty(this, x => x.TotalTime);

            this.totalSeconds = this.library.TotalTime
                .Select(x => (int)x.TotalSeconds)
                .ToProperty(this, x => x.TotalSeconds);

            this.AddPlaylistCommand = new ReactiveCommand(this.WhenAnyValue(x => x.CanSwitchPlaylist));
            this.AddPlaylistCommand.Subscribe(x => this.AddPlaylist());

            this.Playlists = this.library.Playlists.CreateDerivedCollection(this.CreatePlaylistViewModel);
            this.Playlists.ItemsRemoved.Subscribe(x => x.Dispose());

            this.ShowSettingsCommand = new ReactiveCommand();
            this.ShowSettingsCommand.Subscribe(x => this.SettingsViewModel.HandleSettings());

            this.ShufflePlaylistCommand = new ReactiveCommand();
            this.ShufflePlaylistCommand.Subscribe(x => this.library.ShufflePlaylist());

            this.PlayCommand = new ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries)
                .CombineLatest(isAdminObservable, this.library.LockPlayPause, this.library.LoadedSong, this.library.PlaybackState,
                    (selectedPlaylistEntries, isAdmin, lockPlayPause, loadedSong, playBackState) =>

                        // The admin can always play, but if we are in party mode, we have to check whether it is allowed to play
                        (isAdmin || !lockPlayPause) &&

                        // If exactly one song is selected, the command can be executed
                        (selectedPlaylistEntries != null && selectedPlaylistEntries.Count() == 1 ||

                        // If the current song is paused, the command can be executed
                        (loadedSong != null || playBackState == AudioPlayerState.Paused))));
            this.PlayCommand.Subscribe(async x =>
            {
                if (await this.library.PlaybackState.FirstAsync() == AudioPlayerState.Paused || await this.library.LoadedSong.FirstAsync() != null)
                {
                    await this.library.ContinueSongAsync();
                }

                else
                {
                    await this.library.PlaySongAsync(this.SelectedPlaylistEntries.First().Index);
                }
            });

            this.PlayOverrideCommand = new ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries)
                .CombineLatest(this.isAdmin, this.library.LockPlayPause, (selectedPlaylistEntries, isAdmin, lockPlayPause) =>
                    (isAdmin || !lockPlayPause) && (selectedPlaylistEntries != null && selectedPlaylistEntries.Count() == 1)));
            this.PlayOverrideCommand.RegisterAsyncTask(_ => this.library.PlaySongAsync(this.SelectedPlaylistEntries.First().Index));

            this.PauseCommand = new ReactiveCommand(this.isAdmin.CombineLatest(this.library.LockPlayPause, this.isPlaying,
                (isAdmin, lockPlayPause, isPlaying) => (isAdmin || !lockPlayPause) && isPlaying));
            this.PauseCommand.RegisterAsyncTask(_ => this.library.PauseSongAsync());

            this.PauseContinueCommand = new ReactiveCommand(this
                .WhenAnyValue(x => x.IsPlaying)
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
                .WhenAnyValue(x => x.CanSwitchPlaylist, x => x.CurrentPlaylist, (x1, x2) => x1 && !x2.Model.IsTemporary);
            this.EditPlaylistNameCommand = new ReactiveCommand(canEditPlaylist);
            this.EditPlaylistNameCommand.Subscribe(x => this.CurrentPlaylist.EditName = true);

            this.RemovePlaylistCommand = new ReactiveCommand(this.WhenAnyValue(x => x.CurrentEditedPlaylist, x => x.CurrentPlaylist, (x1, x2) => x1 != null || x2 != null));
            this.RemovePlaylistCommand.Subscribe(x =>
            {
                int index = this.Playlists.TakeWhile(p => p != this.CurrentPlaylist).Count();

                this.library.RemovePlaylist(this.CurrentPlaylist.Model);

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
            });

            this.RemoveSelectedPlaylistEntriesCommand = new ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries)
                .CombineLatest(this.isAdmin, this.library.LockPlaylistRemoval,
                    (selectedPlaylistEntries, isAdmin, lockPlaylistRemoval) =>
                        selectedPlaylistEntries != null && selectedPlaylistEntries.Any() && (isAdmin || lockPlaylistRemoval)));
            this.RemoveSelectedPlaylistEntriesCommand.Subscribe(x => this.library.RemoveFromPlaylist(this.SelectedPlaylistEntries.Select(entry => entry.Index)));

            this.IsLocal = true;
        }

        public IReactiveCommand AddPlaylistCommand { get; private set; }

        public IAudioPlayerCallback AudioPlayerCallback
        {
            get { return this.library.AudioPlayerCallback; }
        }

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
            get { return this.Playlists.SingleOrDefault(vm => vm.Model == this.library.CurrentPlaylist); }
            set
            {
                if (value != null) // There always has to be a playlist selected
                {
                    this.library.SwitchToPlaylist(value.Model);
                    this.RaisePropertyChanged();
                }
            }
        }

        public int CurrentSeconds
        {
            get { return this.currentSeconds.Value; }
            set { this.library.CurrentTime = TimeSpan.FromSeconds(value); }
        }

        public ISongSourceViewModel CurrentSongSource
        {
            get { return this.currentSongSource.Value; }
        }

        public TimeSpan CurrentTime
        {
            get { return this.currentTime.Value; }
        }

        public bool DisplayTimeoutWarning
        {
            get { return this.displayTimeoutWarning; }
            set { this.RaiseAndSetIfChanged(ref this.displayTimeoutWarning, value); }
        }

        public IReactiveCommand EditPlaylistNameCommand { get; private set; }

        public bool IsAdmin
        {
            get { return this.isAdmin.Value; }
        }

        public bool IsLocal
        {
            get { return this.isLocal; }
            set { this.RaiseAndSetIfChanged(ref this.isLocal, value); }
        }

        public bool IsPlaying
        {
            get { return this.isPlaying.Value; }
        }

        public bool IsYoutube
        {
            get { return this.isYoutube; }
            set { this.RaiseAndSetIfChanged(ref this.isYoutube, value); }
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
        public IReactiveCommand PlayCommand { get; private set; }

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
            set { Settings.Default.PlaylistHeight = new GridLengthConverter().ConvertToString(value); }
        }

        public IReactiveDerivedList<PlaylistViewModel> Playlists { get; private set; }

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
        /// Overrides the currently played song.
        /// </summary>
        public IReactiveCommand PlayOverrideCommand { get; private set; }

        /// <summary>
        /// Plays the song that is before the currently played song in the playlist.
        /// </summary>
        public IReactiveCommand PreviousSongCommand { get; private set; }

        public TimeSpan RemainingPlaylistTimeout
        {
            get { return this.library.RemainingPlaylistTimeout; }
        }

        public IReactiveCommand RemovePlaylistCommand { get; private set; }

        public IReactiveCommand RemoveSelectedPlaylistEntriesCommand { get; private set; }

        public IEnumerable<PlaylistEntryViewModel> SelectedPlaylistEntries
        {
            get { return this.selectedPlaylistEntries; }
            set { this.RaiseAndSetIfChanged(ref this.selectedPlaylistEntries, value); }
        }

        public SettingsViewModel SettingsViewModel { get; private set; }

        public bool ShowPlaylistTimeout
        {
            get { return this.showPlaylistTimeout.Value; }
        }

        public IReactiveCommand ShowSettingsCommand { get; private set; }

        public bool ShowVideoPlayer
        {
            get { return this.showVideoPlayer; }
            set { this.RaiseAndSetIfChanged(ref this.showVideoPlayer, value); }
        }

        public IReactiveCommand ShufflePlaylistCommand { get; private set; }

        public GridLength SongSourceHeight
        {
            get { return (GridLength)new GridLengthConverter().ConvertFromString(Settings.Default.SongSourceHeight); }
            set { Settings.Default.SongSourceHeight = new GridLengthConverter().ConvertToString(value); }
        }

        public int TotalSeconds
        {
            get { return this.totalSeconds.Value; }
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

        public double Volume
        {
            get { return this.library.Volume; }
            set
            {
                this.library.Volume = (float)value;
                this.RaisePropertyChanged();
            }
        }

        public YoutubeViewModel YoutubeViewModel { get; private set; }

        public void Dispose()
        {
            Settings.Default.Save();

            this.library.Save();
            this.library.Dispose();

            this.playlistTimeoutUpdateTimer.Dispose();
        }

        private void AddPlaylist()
        {
            this.library.AddAndSwitchToPlaylist(this.GetNewPlaylistName());

            this.CurrentPlaylist = this.Playlists.Last();
            this.CurrentPlaylist.EditName = true;
        }

        private PlaylistViewModel CreatePlaylistViewModel(Playlist playlist)
        {
            return new PlaylistViewModel(playlist, name => this.Playlists.Count(p => p.Name == name) == 1);
        }

        private string GetNewPlaylistName()
        {
            string newName = (this.Playlists ?? Enumerable.Empty<PlaylistViewModel>())
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

        private void TriggerTimeoutWarning()
        {
            this.DisplayTimeoutWarning = true;
            this.DisplayTimeoutWarning = false;
        }

        private void UpdateRemainingPlaylistTimeout()
        {
            if (this.RemainingPlaylistTimeout > TimeSpan.Zero)
            {
                this.RaisePropertyChanged("RemainingPlaylistTimeout");
            }
        }
    }
}