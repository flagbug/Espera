using System;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Mobile;
using Espera.Core.Settings;

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

        public ShellViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings,
            IWindowManager windowManager, MobileApiInfo mobileApiInfo)
        {
            this.library = library;
            ViewSettings = viewSettings;
            this.coreSettings = coreSettings;

            disposable = new CompositeDisposable();
            UpdateViewModel = new UpdateViewModel(viewSettings);

            this.library.Initialize();
            accessToken = this.library.LocalAccessControl.RegisterLocalAccessToken();

            this.library.WhenAnyValue(x => x.CurrentPlaylist)
                .Subscribe(x => this.RaisePropertyChanged("CurrentPlaylist"));

            canChangeTime = this.library.LocalAccessControl
                .HasAccess(this.coreSettings.WhenAnyValue(x => x.LockTime), accessToken)
                .ToProperty(this, x => x.CanChangeTime);
            canChangeVolume = this.library.LocalAccessControl
                .HasAccess(this.coreSettings.WhenAnyValue(x => x.LockVolume), accessToken)
                .ToProperty(this, x => x.CanChangeVolume);
            this.canAlterPlaylist = this.library.LocalAccessControl
                .HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlaylist), accessToken)
                .ToProperty(this, x => x.CanAlterPlaylist);

            showVotes = this.library.RemoteAccessControl.WhenAnyValue(x => x.IsGuestSystemReallyEnabled)
                .CombineLatest(mobileApiInfo.ConnectedClientCount,
                    (enableGuestSystem, connectedClients) => enableGuestSystem && connectedClients > 0)
                .ToProperty(this, x => x.ShowVotes);

            mobileApiInfo.VideoPlayerToggleRequest.Subscribe(_ => ShowVideoPlayer = !ShowVideoPlayer);

            isAdmin = this.library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin)
                .ToProperty(this, x => x.IsAdmin);

            NextSongCommand = ReactiveCommand.CreateAsyncTask(this.library.LocalAccessControl
                    .HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause), accessToken)
                    .CombineLatest(this.library.WhenAnyValue(x => x.CurrentPlaylist.CanPlayNextSong),
                        (x1, x2) => x1 && x2)
                    .ObserveOn(RxApp.MainThreadScheduler),
                _ => this.library.PlayNextSongAsync(accessToken));

            PreviousSongCommand = ReactiveCommand.CreateAsyncTask(this.library.LocalAccessControl
                    .HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause), accessToken)
                    .CombineLatest(this.library.WhenAnyValue(x => x.CurrentPlaylist.CanPlayPreviousSong),
                        (x1, x2) => x1 && x2)
                    .ObserveOn(RxApp.MainThreadScheduler),
                _ => this.library.PlayPreviousSongAsync(accessToken));

            if (!this.library.Playlists.Any())
                this.library.AddAndSwitchToPlaylist(GetNewPlaylistName(), accessToken);

            else
                this.library.SwitchToPlaylist(this.library.Playlists.First(), accessToken);

            SettingsViewModel = new SettingsViewModel(this.library, ViewSettings, this.coreSettings, windowManager,
                accessToken, mobileApiInfo);

            LocalViewModel = new LocalViewModel(this.library, ViewSettings, this.coreSettings, accessToken);
            YoutubeViewModel = new YoutubeViewModel(this.library, ViewSettings, this.coreSettings, accessToken);
            SoundCloudViewModel = new SoundCloudViewModel(this.library, accessToken, this.coreSettings, ViewSettings);
            DirectYoutubeViewModel = new DirectYoutubeViewModel(this.library, this.coreSettings, accessToken);

            currentSongSource = this.WhenAnyValue(x => x.IsLocal, x => x.IsYoutube, x => x.IsSoundCloud,
                    (local, youtube, soundcloud) =>
                    {
                        if (local) return (ISongSourceViewModel)LocalViewModel;

                        if (youtube) return YoutubeViewModel;

                        if (soundcloud) return SoundCloudViewModel;

                        return LocalViewModel;
                    })
                .ToProperty(this, x => x.CurrentSongSource, null, ImmediateScheduler.Instance);

            MuteCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.IsAdmin));
            MuteCommand.Subscribe(x => Volume = 0);

            UnMuteCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.IsAdmin));
            UnMuteCommand.Subscribe(x => Volume = 1);

            canModifyWindow = this.library.LocalAccessControl
                .HasAccess(ViewSettings.WhenAnyValue(x => x.LockWindow), accessToken)
                .ToProperty(this, x => x.CanModifyWindow);

            this.isPlaying = this.library.PlaybackState
                .Select(x => x == AudioPlayerState.Playing)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsPlaying);

            currentTime = this.library.CurrentPlaybackTime
                .StartWith(TimeSpan.Zero)
                .Select(x => x.FormatAdaptive())
                .ToProperty(this, x => x.CurrentTime);

            currentSeconds = this.library.CurrentPlaybackTime
                .Select(x => (int)x.TotalSeconds)
                .ToProperty(this, x => x.CurrentSeconds);

            totalTime = this.library.TotalTime
                .Select(x => x.FormatAdaptive())
                .ToProperty(this, x => x.TotalTime);

            totalSeconds = this.library.TotalTime
                .Select(x => (int)x.TotalSeconds)
                .ToProperty(this, x => x.TotalSeconds);

            volume = this.library.WhenAnyValue(x => x.Volume, x => (double)x)
                .ToProperty(this, x => x.Volume);

            AddPlaylistCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanAlterPlaylist));
            AddPlaylistCommand.Subscribe(x => AddPlaylist());

            Playlists = this.library.Playlists.CreateDerivedCollection(CreatePlaylistViewModel, x => x.Dispose());

            ShowSettingsCommand = ReactiveCommand.Create();
            ShowSettingsCommand.Subscribe(x => SettingsViewModel.HandleSettings());

            ShufflePlaylistCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanAlterPlaylist));
            ShufflePlaylistCommand.Subscribe(x => this.library.ShufflePlaylist(accessToken));

            var canPlay = this.WhenAnyValue(x => x.CurrentPlaylist.SelectedEntries)
                .CombineLatest(
                    this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause),
                        accessToken), this.library.LoadedSong, this.library.PlaybackState,
                    (selectedPlaylistEntries, hasPlayAccess, loadedSong, playBackState) =>

                        // The admin can always play, but if we are in party mode, we have to check
                        // whether it is allowed to play
                        hasPlayAccess &&

                        // If exactly one song is selected, the command can be executed
                        ((selectedPlaylistEntries != null && selectedPlaylistEntries.Count() == 1) ||
                         loadedSong != null || playBackState == AudioPlayerState.Paused));
            PlayCommand = ReactiveCommand.CreateAsyncTask(canPlay, async _ =>
            {
                if (await this.library.PlaybackState.FirstAsync() == AudioPlayerState.Paused ||
                    await this.library.LoadedSong.FirstAsync() != null)
                    await this.library.ContinueSongAsync(accessToken);

                else
                    await this.library.PlaySongAsync(CurrentPlaylist.SelectedEntries.First().Index, accessToken);
            });

            PlayOverrideCommand = ReactiveCommand.CreateAsyncTask(this
                    .WhenAnyValue(x => x.CurrentPlaylist.SelectedEntries)
                    .CombineLatest(
                        this.library.LocalAccessControl.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause),
                            accessToken),
                        (selectedPlaylistEntries, hasAccess) => hasAccess && selectedPlaylistEntries != null &&
                                                                selectedPlaylistEntries.Count() == 1),
                _ => this.library.PlaySongAsync(CurrentPlaylist.SelectedEntries.First().Index, accessToken));

            PauseCommand = ReactiveCommand.CreateAsyncTask(this.library.LocalAccessControl
                    .HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause), accessToken)
                    .CombineLatest(this.WhenAnyValue(x => x.IsPlaying),
                        (hasAccess, isPlaying) => hasAccess && isPlaying),
                _ => this.library.PauseSongAsync(accessToken));

            var pauseOrContinueCommand = this.WhenAnyValue(x => x.IsPlaying)
                .Select(x => x ? PauseCommand : PlayCommand).Publish(null);
            pauseOrContinueCommand.Connect();

            PauseContinueCommand = ReactiveCommand.CreateAsyncTask(
                pauseOrContinueCommand.Select(x => x.CanExecuteObservable).Switch()
                    .ObserveOn(RxApp.MainThreadScheduler),
                async _ =>
                {
                    IReactiveCommand<Unit> command = await pauseOrContinueCommand.FirstAsync();
                    await command.ExecuteAsync();
                });

            EditPlaylistNameCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanAlterPlaylist,
                x => x.CurrentPlaylist, (x1, x2) => x1 && !x2.Model.IsTemporary));
            EditPlaylistNameCommand.Subscribe(x => CurrentPlaylist.EditName = true);

            RemovePlaylistCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CurrentEditedPlaylist,
                x => x.CurrentPlaylist, x => x.CanAlterPlaylist,
                (currentEditedPlaylist, currentPlaylist, canAlterPlaylist) =>
                    (currentEditedPlaylist != null || currentPlaylist != null) && canAlterPlaylist));
            RemovePlaylistCommand.Subscribe(x => RemoveCurrentPlaylist());

            IsLocal = true;
        }

        public ReactiveCommand<object> AddPlaylistCommand { get; }

        public bool CanAlterPlaylist => canAlterPlaylist.Value;

        public bool CanChangeTime => canChangeTime.Value;

        public bool CanChangeVolume => canChangeVolume.Value;

        /// <summary>
        ///     Gets a value indicating whether the window can be minimized, maximized or closed
        /// </summary>
        public bool CanModifyWindow => canModifyWindow.Value;

        public PlaylistViewModel CurrentEditedPlaylist
        {
            get { return Playlists.SingleOrDefault(playlist => playlist.EditName); }
        }

        public PlaylistViewModel CurrentPlaylist
        {
            get { return Playlists.SingleOrDefault(vm => vm.Model == library.CurrentPlaylist); }
            set
            {
                if (value != null) // There always has to be a playlist selected
                {
                    library.SwitchToPlaylist(value.Model, accessToken);
                    this.RaisePropertyChanged();
                }
            }
        }

        public int CurrentSeconds
        {
            get => currentSeconds.Value;
            set => library.SetCurrentTime(TimeSpan.FromSeconds(value), accessToken);
        }

        public ISongSourceViewModel CurrentSongSource => currentSongSource.Value;

        public string CurrentTime => currentTime.Value;

        public DirectYoutubeViewModel DirectYoutubeViewModel { get; }

        public ReactiveCommand<object> EditPlaylistNameCommand { get; }

        public bool IsAdmin => isAdmin.Value;

        public bool IsLocal
        {
            get => isLocal;
            set => this.RaiseAndSetIfChanged(ref isLocal, value);
        }

        public bool IsPlaying => isPlaying.Value;

        public bool IsSoundCloud
        {
            get => isSoundCloud;
            set => this.RaiseAndSetIfChanged(ref isSoundCloud, value);
        }

        public bool IsYoutube
        {
            get => isYoutube;
            set => this.RaiseAndSetIfChanged(ref isYoutube, value);
        }

        public LocalViewModel LocalViewModel { get; }

        /// <summary>
        ///     Sets the volume to the lowest possible value.
        /// </summary>
        public ReactiveCommand<object> MuteCommand { get; }

        /// <summary>
        ///     Plays the next song in the playlist.
        /// </summary>
        public ReactiveCommand<Unit> NextSongCommand { get; }

        /// <summary>
        ///     Pauses the currently played song.
        /// </summary>
        public ReactiveCommand<Unit> PauseCommand { get; }

        /// <summary>
        ///     A command that decides whether the songs should be paused or continued.
        /// </summary>
        public ReactiveCommand<Unit> PauseContinueCommand { get; }

        /// <summary>
        ///     Plays the song that is currently selected in the playlist or continues the song if it is paused.
        /// </summary>
        public ReactiveCommand<Unit> PlayCommand { get; }

        public IReactiveDerivedList<PlaylistViewModel> Playlists { get; }

        /// <summary>
        ///     Overrides the currently played song.
        /// </summary>
        public ReactiveCommand<Unit> PlayOverrideCommand { get; }

        /// <summary>
        ///     Plays the song that is before the currently played song in the playlist.
        /// </summary>
        public ReactiveCommand<Unit> PreviousSongCommand { get; }

        public ReactiveCommand<object> RemovePlaylistCommand { get; }

        public SettingsViewModel SettingsViewModel { get; }

        public ReactiveCommand<object> ShowSettingsCommand { get; }

        public bool ShowVideoPlayer
        {
            get => showVideoPlayer;
            set => this.RaiseAndSetIfChanged(ref showVideoPlayer, value);
        }

        public bool ShowVotes => showVotes.Value;

        public ReactiveCommand<object> ShufflePlaylistCommand { get; }

        public SoundCloudViewModel SoundCloudViewModel { get; }

        public int TotalSeconds => totalSeconds.Value;

        public string TotalTime => totalTime.Value;

        /// <summary>
        ///     Sets the volume to the highest possible value.
        /// </summary>
        public ReactiveCommand<object> UnMuteCommand { get; }

        /// <summary>
        ///     Occurs when the view should update the screen state to maximized state or restore it to
        ///     normal state
        /// </summary>
        public IObservable<AccessPermission> UpdateScreenState =>
            library.LocalAccessControl.ObserveAccessPermission(accessToken);

        public UpdateViewModel UpdateViewModel { get; }

        public ViewSettings ViewSettings { get; }

        public double Volume
        {
            get => volume.Value;
            set
            {
                library.SetVolume((float)value, accessToken);
                this.RaisePropertyChanged();
            }
        }

        public YoutubeViewModel YoutubeViewModel { get; }

        public void Dispose()
        {
            library.Save();
            library.Dispose();

            UpdateViewModel.Dispose();

            disposable.Dispose();
        }

        public void RegisterAudioPlayer(IMediaPlayerCallback callback)
        {
            library.RegisterAudioPlayerCallback(callback, accessToken);
        }

        public void RegisterVideoPlayer(IMediaPlayerCallback callback)
        {
            library.RegisterVideoPlayerCallback(callback, accessToken);
        }

        private void AddPlaylist()
        {
            library.AddAndSwitchToPlaylist(GetNewPlaylistName(), accessToken);

            CurrentPlaylist = Playlists.Last();
            CurrentPlaylist.EditName = true;
        }

        private PlaylistViewModel CreatePlaylistViewModel(Playlist playlist)
        {
            return new PlaylistViewModel(playlist, library, accessToken, coreSettings);
        }

        private string GetNewPlaylistName()
        {
            var newName = (Playlists ?? Enumerable.Empty<PlaylistViewModel>())
                .Select(playlist => playlist.Name)
                .CreateUnique(i =>
                {
                    var name = "New Playlist";

                    if (i > 1) name += " " + i;

                    return name;
                });

            return newName;
        }

        private void RemoveCurrentPlaylist()
        {
            var index = Playlists.TakeWhile(p => p != CurrentPlaylist).Count();

            library.RemovePlaylist(CurrentPlaylist.Model, accessToken);

            if (!library.Playlists.Any()) AddPlaylist();

            if (Playlists.Count > index)
                CurrentPlaylist = Playlists[index];

            else if (Playlists.Count >= 1)
                CurrentPlaylist = Playlists[index - 1];

            else
                CurrentPlaylist = Playlists[0];
        }
    }
}