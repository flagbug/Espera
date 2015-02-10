using Caliburn.Micro;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Mobile;
using Espera.Core.Settings;
using Rareform.Extensions;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Espera.View.ViewModels
{
    internal class ShellViewModel : ReactiveObject, IDisposable
    {
        private readonly Guid accessToken;
        private readonly ObservableAsPropertyHelper<bool> canAlterPlaylist;
        private readonly ObservableAsPropertyHelper<bool> canChangeTime;
        private readonly ObservableAsPropertyHelper<bool> canChangeVolume;
        private readonly ObservableAsPropertyHelper<bool> canModifyWindow;
        private readonly CoreSettings coreSettings;
        private readonly ObservableAsPropertyHelper<int> currentSeconds;
        private readonly ObservableAsPropertyHelper<ISongSourceViewModel> currentSongSource;
        private readonly ObservableAsPropertyHelper<string> currentTime;
        private readonly CompositeDisposable disposable;
        private readonly ObservableAsPropertyHelper<bool> isAdmin;
        private readonly ObservableAsPropertyHelper<bool> isPlaying;
        private readonly Library library;
        private readonly ObservableAsPropertyHelper<bool> showVotes;
        private readonly ObservableAsPropertyHelper<int> totalSeconds;
        private readonly ObservableAsPropertyHelper<string> totalTime;
        private readonly ObservableAsPropertyHelper<double> volume;
        private bool isLocal;
        private bool isSoundCloud;
        private bool isYoutube;
        private bool showVideoPlayer;

        public ShellViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, IWindowManager windowManager, MobileApiInfo mobileApiInfo)
        {
            this.library = library;
            this.ViewSettings = viewSettings;
            this.coreSettings = coreSettings;

            this.disposable = new CompositeDisposable();
            this.UpdateViewModel = new UpdateViewModel(viewSettings);

            this.library.Initialize();
            this.accessToken = this.library.LocalAccessControl.RegisterLocalAccessToken();

            this.library.WhenAnyValue(x => x.CurrentPlaylist).Subscribe(x => this.RaisePropertyChanged("CurrentPlaylist"));

            this.canChangeTime = this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockTime), this.accessToken)
                .ToProperty(this, x => x.CanChangeTime);
            this.canChangeVolume = this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockVolume), this.accessToken)
                .ToProperty(this, x => x.CanChangeVolume);
            this.canAlterPlaylist = this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlaylist), this.accessToken)
                .ToProperty(this, x => x.CanAlterPlaylist);

            this.showVotes = this.library.RemoteAccessControl.WhenAnyValue(x => x.IsGuestSystemReallyEnabled)
                .CombineLatest(mobileApiInfo.ConnectedClientCount, (enableGuestSystem, connectedClients) => enableGuestSystem && connectedClients > 0)
                .ToProperty(this, x => x.ShowVotes);

            mobileApiInfo.VideoPlayerToggleRequest.Subscribe(_ => this.ShowVideoPlayer = !this.ShowVideoPlayer);

            this.isAdmin = this.library.LocalAccessControl.ObserveAccessPermission(this.accessToken)
                .Select(x => x == AccessPermission.Admin)
                .ToProperty(this, x => x.IsAdmin);

            this.NextSongCommand = ReactiveCommand.CreateAsyncTask(this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause), this.accessToken)
                    .CombineLatest(this.library.WhenAnyValue(x => x.CurrentPlaylist.CanPlayNextSong), (x1, x2) => x1 && x2)
                    .ObserveOn(RxApp.MainThreadScheduler),
                _ => this.library.PlayNextSongAsync(this.accessToken));

            this.PreviousSongCommand = ReactiveCommand.CreateAsyncTask(this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause), this.accessToken)
                    .CombineLatest(this.library.WhenAnyValue(x => x.CurrentPlaylist.CanPlayPreviousSong), (x1, x2) => x1 && x2)
                    .ObserveOn(RxApp.MainThreadScheduler),
                _ => this.library.PlayPreviousSongAsync(this.accessToken));

            if (!this.library.Playlists.Any())
            {
                this.library.AddAndSwitchToPlaylist(this.GetNewPlaylistName(), this.accessToken);
            }

            else
            {
                this.library.SwitchToPlaylist(this.library.Playlists.First(), this.accessToken);
            }

            this.SettingsViewModel = new SettingsViewModel(this.library, this.ViewSettings, this.coreSettings, windowManager, this.accessToken, mobileApiInfo);

            this.LocalViewModel = new LocalViewModel(this.library, this.ViewSettings, this.coreSettings, accessToken);
            this.YoutubeViewModel = new YoutubeViewModel(this.library, this.ViewSettings, this.coreSettings, accessToken);
            this.SoundCloudViewModel = new SoundCloudViewModel(this.library, accessToken, this.coreSettings, this.ViewSettings);
            this.DirectYoutubeViewModel = new DirectYoutubeViewModel(this.library, this.coreSettings, accessToken);

            this.currentSongSource = this.WhenAnyValue(x => x.IsLocal, x => x.IsYoutube, x => x.IsSoundCloud,
                (local, youtube, soundcloud) =>
                {
                    if (local)
                    {
                        return (ISongSourceViewModel)this.LocalViewModel;
                    }

                    if (youtube)
                    {
                        return this.YoutubeViewModel;
                    }

                    if (soundcloud)
                    {
                        return this.SoundCloudViewModel;
                    }

                    return this.LocalViewModel;
                })
                .ToProperty(this, x => x.CurrentSongSource, null, ImmediateScheduler.Instance);

            this.MuteCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.IsAdmin));
            this.MuteCommand.Subscribe(x => this.Volume = 0);

            this.UnMuteCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.IsAdmin));
            this.UnMuteCommand.Subscribe(x => this.Volume = 1);

            this.canModifyWindow = this.library.LocalAccessControl.HasAccess(this.ViewSettings.WhenAnyValue(x => x.LockWindow), this.accessToken)
                .ToProperty(this, x => x.CanModifyWindow);

            this.isPlaying = this.library.PlaybackState
                .Select(x => x == AudioPlayerState.Playing)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsPlaying);

            this.currentTime = this.library.CurrentPlaybackTime
                .StartWith(TimeSpan.Zero)
                .Select(x => x.FormatAdaptive())
                .ToProperty(this, x => x.CurrentTime);

            this.currentSeconds = this.library.CurrentPlaybackTime
                .Select(x => (int)x.TotalSeconds)
                .ToProperty(this, x => x.CurrentSeconds);

            this.totalTime = this.library.TotalTime
                .Select(x => x.FormatAdaptive())
                .ToProperty(this, x => x.TotalTime);

            this.totalSeconds = this.library.TotalTime
                .Select(x => (int)x.TotalSeconds)
                .ToProperty(this, x => x.TotalSeconds);

            this.volume = this.library.WhenAnyValue(x => x.Volume, x => (double)x)
                .ToProperty(this, x => x.Volume);

            this.AddPlaylistCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanAlterPlaylist));
            this.AddPlaylistCommand.Subscribe(x => this.AddPlaylist());

            this.Playlists = this.library.Playlists.CreateDerivedCollection(this.CreatePlaylistViewModel, x => x.Dispose());

            this.ShowSettingsCommand = ReactiveCommand.Create();
            this.ShowSettingsCommand.Subscribe(x => this.SettingsViewModel.HandleSettings());

            this.ShufflePlaylistCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanAlterPlaylist));
            this.ShufflePlaylistCommand.Subscribe(x => this.library.ShufflePlaylist(this.accessToken));

            IObservable<bool> canPlay = this.WhenAnyValue(x => x.CurrentPlaylist.SelectedEntries)
                .CombineLatest(this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause), this.accessToken), this.library.LoadedSong, this.library.PlaybackState,
                    (selectedPlaylistEntries, hasPlayAccess, loadedSong, playBackState) =>

                        // The admin can always play, but if we are in party mode, we have to check
                        // whether it is allowed to play
                        hasPlayAccess &&

                        // If exactly one song is selected, the command can be executed
                        (selectedPlaylistEntries != null && selectedPlaylistEntries.Count() == 1 ||

                        // If the current song is paused, the command can be executed
                        (loadedSong != null || playBackState == AudioPlayerState.Paused)));
            this.PlayCommand = ReactiveCommand.CreateAsyncTask(canPlay, async _ =>
            {
                if (await this.library.PlaybackState.FirstAsync() == AudioPlayerState.Paused || await this.library.LoadedSong.FirstAsync() != null)
                {
                    await this.library.ContinueSongAsync(this.accessToken);
                }

                else
                {
                    await this.library.PlaySongAsync(this.CurrentPlaylist.SelectedEntries.First().Index, this.accessToken);
                }
            });

            this.PlayOverrideCommand = ReactiveCommand.CreateAsyncTask(this.WhenAnyValue(x => x.CurrentPlaylist.SelectedEntries)
                .CombineLatest(this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause), this.accessToken),
                    (selectedPlaylistEntries, hasAccess) => hasAccess && (selectedPlaylistEntries != null && selectedPlaylistEntries.Count() == 1)),
                _ => this.library.PlaySongAsync(this.CurrentPlaylist.SelectedEntries.First().Index, this.accessToken));

            this.PauseCommand = ReactiveCommand.CreateAsyncTask(this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause), this.accessToken)
                .CombineLatest(this.WhenAnyValue(x => x.IsPlaying), (hasAccess, isPlaying) => hasAccess && isPlaying),
                _ => this.library.PauseSongAsync(this.accessToken));

            var pauseOrContinueCommand = this.WhenAnyValue(x => x.IsPlaying)
                .Select(x => x ? this.PauseCommand : this.PlayCommand).Publish(null);
            pauseOrContinueCommand.Connect();

            this.PauseContinueCommand = ReactiveCommand.CreateAsyncTask(
                pauseOrContinueCommand.Select(x => x.CanExecuteObservable).Switch().ObserveOn(RxApp.MainThreadScheduler),
                async _ =>
                {
                    IReactiveCommand<Unit> command = await pauseOrContinueCommand.FirstAsync();
                    await command.ExecuteAsync();
                });

            this.EditPlaylistNameCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanAlterPlaylist, x => x.CurrentPlaylist, (x1, x2) => x1 && !x2.Model.IsTemporary));
            this.EditPlaylistNameCommand.Subscribe(x => this.CurrentPlaylist.EditName = true);

            this.RemovePlaylistCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CurrentEditedPlaylist, x => x.CurrentPlaylist, x => x.CanAlterPlaylist,
                    (currentEditedPlaylist, currentPlaylist, canAlterPlaylist) => (currentEditedPlaylist != null || currentPlaylist != null) && canAlterPlaylist));
            this.RemovePlaylistCommand.Subscribe(x => this.RemoveCurrentPlaylist());

            this.IsLocal = true;
        }

        public ReactiveCommand<object> AddPlaylistCommand { get; private set; }

        public bool CanAlterPlaylist
        {
            get { return this.canAlterPlaylist.Value; }
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
                    this.library.SwitchToPlaylist(value.Model, this.accessToken);
                    this.RaisePropertyChanged();
                }
            }
        }

        public int CurrentSeconds
        {
            get { return this.currentSeconds.Value; }
            set { this.library.SetCurrentTime(TimeSpan.FromSeconds(value), this.accessToken); }
        }

        public ISongSourceViewModel CurrentSongSource
        {
            get { return this.currentSongSource.Value; }
        }

        public string CurrentTime
        {
            get { return this.currentTime.Value; }
        }

        public DirectYoutubeViewModel DirectYoutubeViewModel { get; private set; }

        public ReactiveCommand<object> EditPlaylistNameCommand { get; private set; }

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

        public bool IsSoundCloud
        {
            get { return this.isSoundCloud; }
            set { this.RaiseAndSetIfChanged(ref this.isSoundCloud, value); }
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
        public ReactiveCommand<object> MuteCommand { get; private set; }

        /// <summary>
        /// Plays the next song in the playlist.
        /// </summary>
        public ReactiveCommand<Unit> NextSongCommand { get; private set; }

        /// <summary>
        /// Pauses the currently played song.
        /// </summary>
        public ReactiveCommand<Unit> PauseCommand { get; private set; }

        /// <summary>
        /// A command that decides whether the songs should be paused or continued.
        /// </summary>
        public ReactiveCommand<Unit> PauseContinueCommand { get; private set; }

        /// <summary>
        /// Plays the song that is currently selected in the playlist or continues the song if it is paused.
        /// </summary>
        public ReactiveCommand<Unit> PlayCommand { get; private set; }

        public IReactiveDerivedList<PlaylistViewModel> Playlists { get; private set; }

        /// <summary>
        /// Overrides the currently played song.
        /// </summary>
        public ReactiveCommand<Unit> PlayOverrideCommand { get; private set; }

        /// <summary>
        /// Plays the song that is before the currently played song in the playlist.
        /// </summary>
        public ReactiveCommand<Unit> PreviousSongCommand { get; private set; }

        public ReactiveCommand<object> RemovePlaylistCommand { get; private set; }

        public SettingsViewModel SettingsViewModel { get; private set; }

        public ReactiveCommand<object> ShowSettingsCommand { get; private set; }

        public bool ShowVideoPlayer
        {
            get { return this.showVideoPlayer; }
            set { this.RaiseAndSetIfChanged(ref this.showVideoPlayer, value); }
        }

        public bool ShowVotes
        {
            get { return this.showVotes.Value; }
        }

        public ReactiveCommand<object> ShufflePlaylistCommand { get; private set; }

        public SoundCloudViewModel SoundCloudViewModel { get; private set; }

        public int TotalSeconds
        {
            get { return this.totalSeconds.Value; }
        }

        public string TotalTime
        {
            get { return this.totalTime.Value; }
        }

        /// <summary>
        /// Sets the volume to the highest possible value.
        /// </summary>
        public ReactiveCommand<object> UnMuteCommand { get; private set; }

        /// <summary>
        /// Occurs when the view should update the screen state to maximized state or restore it to
        /// normal state
        /// </summary>
        public IObservable<AccessPermission> UpdateScreenState
        {
            get { return this.library.LocalAccessControl.ObserveAccessPermission(this.accessToken); }
        }

        public UpdateViewModel UpdateViewModel { get; private set; }

        public ViewSettings ViewSettings { get; private set; }

        public double Volume
        {
            get { return this.volume.Value; }
            set
            {
                this.library.SetVolume((float)value, this.accessToken);
                this.RaisePropertyChanged();
            }
        }

        public YoutubeViewModel YoutubeViewModel { get; private set; }

        public void Dispose()
        {
            this.library.Save();
            this.library.Dispose();

            this.UpdateViewModel.Dispose();

            this.disposable.Dispose();
        }

        public void RegisterAudioPlayer(IMediaPlayerCallback callback)
        {
            this.library.RegisterAudioPlayerCallback(callback, this.accessToken);
        }

        public void RegisterVideoPlayer(IMediaPlayerCallback callback)
        {
            this.library.RegisterVideoPlayerCallback(callback, this.accessToken);
        }

        private void AddPlaylist()
        {
            this.library.AddAndSwitchToPlaylist(this.GetNewPlaylistName(), this.accessToken);

            this.CurrentPlaylist = this.Playlists.Last();
            this.CurrentPlaylist.EditName = true;
        }

        private PlaylistViewModel CreatePlaylistViewModel(Playlist playlist)
        {
            return new PlaylistViewModel(playlist, this.library, this.accessToken, this.coreSettings);
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

        private void RemoveCurrentPlaylist()
        {
            int index = this.Playlists.TakeWhile(p => p != this.CurrentPlaylist).Count();

            this.library.RemovePlaylist(this.CurrentPlaylist.Model, this.accessToken);

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
        }
    }
}