using Caliburn.Micro;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Mobile;
using Espera.Core.Settings;
using Rareform.Extensions;
using ReactiveMarrow;
using ReactiveUI;
using ReactiveUI.Legacy;
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
        private readonly ObservableAsPropertyHelper<ReactiveUI.Legacy.ReactiveCommand> defaultPlaybackCommand;
        private readonly ObservableAsPropertyHelper<bool> displayTimeoutWarning;
        private readonly CompositeDisposable disposable;
        private readonly ObservableAsPropertyHelper<bool> isAdmin;
        private readonly ObservableAsPropertyHelper<bool> isPlaying;
        private readonly Library library;
        private readonly ObservableAsPropertyHelper<bool> showPlaylistTimeout;
        private readonly ObservableAsPropertyHelper<bool> showVotes;
        private readonly ObservableAsPropertyHelper<int> totalSeconds;
        private readonly ObservableAsPropertyHelper<string> totalTime;
        private readonly ObservableAsPropertyHelper<double> volume;
        private bool isLocal;
        private bool isSoundCloud;
        private bool isYoutube;
        private IEnumerable<PlaylistEntryViewModel> selectedPlaylistEntries;
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

            this.library.CurrentPlaylistChanged.Subscribe(x => this.RaisePropertyChanged("CurrentPlaylist"));

            this.canChangeTime = this.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockTime))
                .ToProperty(this, x => x.CanChangeTime);
            this.canChangeVolume = this.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockVolume))
                .ToProperty(this, x => x.CanChangeVolume);
            this.canAlterPlaylist = this.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlaylist))
                .ToProperty(this, x => x.CanAlterPlaylist);

            this.showVotes = this.coreSettings.WhenAnyValue(x => x.EnableVotingSystem)
                .CombineLatest(mobileApiInfo.ConnectedClientCount, (enableVoting, connectedClients) => enableVoting && connectedClients > 0)
                .ToProperty(this, x => x.ShowVotes);

            this.isAdmin = this.library.LocalAccessControl.ObserveAccessPermission(this.accessToken)
                .Select(x => x == AccessPermission.Admin)
                .ToProperty(this, x => x.IsAdmin);

            this.NextSongCommand = new ReactiveUI.Legacy.ReactiveCommand(this.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause))
                .CombineLatest(this.library.CanPlayNextSong, (x1, x2) => x1 && x2));
            this.NextSongCommand.RegisterAsyncTask(_ => this.library.PlayNextSongAsync(this.accessToken));

            this.PreviousSongCommand = new ReactiveUI.Legacy.ReactiveCommand(this.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause))
                .CombineLatest(this.library.CanPlayPreviousSong, (x1, x2) => x1 && x2));
            this.PreviousSongCommand.RegisterAsyncTask(_ => this.library.PlayPreviousSongAsync(this.accessToken));

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
            this.DirectYoutubeViewModel = new DirectYoutubeViewModel(this.library, accessToken);

            Observable.Interval(TimeSpan.FromMilliseconds(300), RxApp.TaskpoolScheduler)
                .Where(_ => this.RemainingPlaylistTimeout > TimeSpan.Zero)
                .Subscribe(x => this.RaisePropertyChanged("RemainingPlaylistTimeout"))
                .DisposeWith(this.disposable);

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

            this.displayTimeoutWarning = Observable.Merge(this.LocalViewModel.TimeoutWarning, this.YoutubeViewModel.TimeoutWarning, this.DirectYoutubeViewModel.TimeoutWarning, this.SoundCloudViewModel.TimeoutWarning)
                .SelectMany(x => new[] { true, false }.ToObservable())
                .ToProperty(this, x => x.DisplayTimeoutWarning);

            this.showPlaylistTimeout = this.WhenAnyValue(x => x.IsAdmin)
                .CombineLatest(this.WhenAnyValue(x => x.SettingsViewModel.EnablePlaylistTimeout), (isAdmin, enableTimeout) => !isAdmin && enableTimeout)
                .ToProperty(this, x => x.ShowPlaylistTimeout);

            this.MuteCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.IsAdmin));
            this.MuteCommand.Subscribe(x => this.Volume = 0);

            this.UnMuteCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.IsAdmin));
            this.UnMuteCommand.Subscribe(x => this.Volume = 1);

            this.canModifyWindow = this.HasAccess(this.ViewSettings.WhenAnyValue(x => x.LockWindow))
                .ToProperty(this, x => x.CanModifyWindow);

            this.isPlaying = this.library.PlaybackState
                .Select(x => x == AudioPlayerState.Playing)
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

            this.AddPlaylistCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.CanAlterPlaylist));
            this.AddPlaylistCommand.Subscribe(x => this.AddPlaylist());

            this.Playlists = this.library.Playlists.CreateDerivedCollection(this.CreatePlaylistViewModel);
            this.Playlists.ItemsRemoved.Subscribe(x => x.Dispose());

            this.ShowSettingsCommand = new ReactiveUI.Legacy.ReactiveCommand();
            this.ShowSettingsCommand.Subscribe(x => this.SettingsViewModel.HandleSettings());

            this.ShufflePlaylistCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.CanAlterPlaylist));
            this.ShufflePlaylistCommand.Subscribe(x => this.library.ShufflePlaylist(this.accessToken));

            this.PlayCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries)
                .CombineLatest(this.WhenAnyValue(x => x.IsAdmin), this.coreSettings.WhenAnyValue(x => x.LockPlayPause), this.library.LoadedSong, this.library.PlaybackState,
                    (selectedPlaylistEntries, isAdmin, lockPlayPause, loadedSong, playBackState) =>

                        // The admin can always play, but if we are in party mode, we have to check
                        // whether it is allowed to play
                        (isAdmin || !lockPlayPause) &&

                        // If exactly one song is selected, the command can be executed
                        (selectedPlaylistEntries != null && selectedPlaylistEntries.Count() == 1 ||

                        // If the current song is paused, the command can be executed
                        (loadedSong != null || playBackState == AudioPlayerState.Paused))));
            this.PlayCommand.SelectMany(async x =>
            {
                if (await this.library.PlaybackState.FirstAsync() == AudioPlayerState.Paused || await this.library.LoadedSong.FirstAsync() != null)
                {
                    await this.library.ContinueSongAsync(this.accessToken);
                }

                else
                {
                    await this.library.PlaySongAsync(this.SelectedPlaylistEntries.First().Index, this.accessToken);
                }

                return Unit.Default;
            }).Subscribe();

            this.PlayOverrideCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries)
                .CombineLatest(this.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause)), (selectedPlaylistEntries, hasAccess) =>
                    hasAccess && (selectedPlaylistEntries != null && selectedPlaylistEntries.Count() == 1)));
            this.PlayOverrideCommand.RegisterAsyncTask(_ => this.library.PlaySongAsync(this.SelectedPlaylistEntries.First().Index, this.accessToken));

            // The default play command differs whether we are in party mode or not and depends on
            // the selected setting in administrator mode and the song source.
            //
            // In party mode, it is always "Add To Playlist", in administrator mode we look at the
            // value that the song source returns
            this.defaultPlaybackCommand = this.WhenAnyValue(x => x.CurrentSongSource, x => x.IsAdmin,
                    (songSource, isAdmin) => !isAdmin || songSource.DefaultPlaybackAction == DefaultPlaybackAction.AddToPlaylist ?
                        songSource.AddToPlaylistCommand : songSource.PlayNowCommand)
                .ToProperty(this, x => x.DefaultPlaybackCommand);

            this.PauseCommand = new ReactiveUI.Legacy.ReactiveCommand(this.HasAccess(this.coreSettings.WhenAnyValue(x => x.LockPlayPause))
                .CombineLatest(this.WhenAnyValue(x => x.IsPlaying), (hasAccess, isPlaying) => hasAccess && isPlaying));
            this.PauseCommand.RegisterAsyncTask(_ => this.library.PauseSongAsync(this.accessToken));

            var pauseOrContinueCommand = this.WhenAnyValue(x => x.IsPlaying)
                .Select(x => x ? this.PauseCommand : this.PlayCommand).Publish(null);
            pauseOrContinueCommand.Connect();

            this.PauseContinueCommand = new ReactiveUI.Legacy.ReactiveCommand(pauseOrContinueCommand.Select(x => x.CanExecuteObservable).Switch());
            this.PauseContinueCommand.SelectMany(async _ => await pauseOrContinueCommand.FirstAsync()).Subscribe(x => x.Execute(null));

            this.EditPlaylistNameCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.CanAlterPlaylist, x => x.CurrentPlaylist, (x1, x2) => x1 && !x2.Model.IsTemporary));
            this.EditPlaylistNameCommand.Subscribe(x => this.CurrentPlaylist.EditName = true);

            this.RemovePlaylistCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.CurrentEditedPlaylist, x => x.CurrentPlaylist, x => x.CanAlterPlaylist,
                    (currentEditedPlaylist, currentPlaylist, canAlterPlaylist) => (currentEditedPlaylist != null || currentPlaylist != null) && canAlterPlaylist));
            this.RemovePlaylistCommand.Subscribe(x => this.RemoveCurrentPlaylist());

            this.RemoveSelectedPlaylistEntriesCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries, x => x.CanAlterPlaylist,
                    (selectedPlaylistEntries, canAlterPlaylist) => selectedPlaylistEntries != null && selectedPlaylistEntries.Any() && canAlterPlaylist));
            this.RemoveSelectedPlaylistEntriesCommand.Subscribe(x => this.library.RemoveFromPlaylist(this.SelectedPlaylistEntries.Select(entry => entry.Index), this.accessToken));

            // We re-evaluate the selected entries after each up or down move here, because WPF
            // doesn't send us proper updates about the selection
            var reEvaluateSelectedPlaylistEntry = new Subject<Unit>();
            this.MovePlaylistSongUpCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => this.SelectedPlaylistEntries))
                .Select(x => x != null && x.Count() == 1 && x.First().Index > 0)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist), (canMoveUp, canAlterPlaylist) => canMoveUp && canAlterPlaylist));
            this.MovePlaylistSongUpCommand.Subscribe(_ =>
            {
                int index = this.SelectedPlaylistEntries.First().Index;
                this.library.MovePlaylistSong(index, index - 1, this.accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            this.MovePlaylistSongDownCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => this.SelectedPlaylistEntries))
                .Select(x => x != null && x.Count() == 1 && x.First().Index < this.CurrentPlaylist.Songs.Count - 1)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist), (canMoveDown, canAlterPlaylist) => canMoveDown && canAlterPlaylist));
            this.MovePlaylistSongDownCommand.Subscribe(_ =>
            {
                int index = this.SelectedPlaylistEntries.First().Index;
                this.library.MovePlaylistSong(index, index + 1, this.accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            this.MovePlaylistSongCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.SelectedPlaylistEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => this.SelectedPlaylistEntries))
                .Select(x => x != null && x.Count() == 1)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist), (canMoveUp, canAlterPlaylist) => canMoveUp && canAlterPlaylist));
            this.MovePlaylistSongCommand.Subscribe(x =>
            {
                int fromIndex = this.SelectedPlaylistEntries.First().Index;
                int toIndex = (int?)x ?? this.CurrentPlaylist.Songs.Last().Index + 1;

                // If we move a song from the front of the playlist to the back, we want it move be
                // in front of the target song
                if (fromIndex < toIndex)
                {
                    toIndex--;
                }

                this.library.MovePlaylistSong(fromIndex, toIndex, this.accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            this.IsLocal = true;
        }

        public ReactiveUI.Legacy.ReactiveCommand AddPlaylistCommand { get; private set; }

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

        public ReactiveUI.Legacy.ReactiveCommand DefaultPlaybackCommand
        {
            get { return this.defaultPlaybackCommand.Value; }
        }

        public DirectYoutubeViewModel DirectYoutubeViewModel { get; private set; }

        public bool DisplayTimeoutWarning
        {
            get { return this.displayTimeoutWarning.Value; }
        }

        public ReactiveUI.Legacy.ReactiveCommand EditPlaylistNameCommand { get; private set; }

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

        public ReactiveUI.Legacy.ReactiveCommand MovePlaylistSongCommand { get; private set; }

        public ReactiveUI.Legacy.ReactiveCommand MovePlaylistSongDownCommand { get; private set; }

        public ReactiveUI.Legacy.ReactiveCommand MovePlaylistSongUpCommand { get; private set; }

        /// <summary>
        /// Sets the volume to the lowest possible value.
        /// </summary>
        public ReactiveUI.Legacy.ReactiveCommand MuteCommand { get; private set; }

        /// <summary>
        /// Plays the next song in the playlist.
        /// </summary>
        public ReactiveUI.Legacy.ReactiveCommand NextSongCommand { get; private set; }

        /// <summary>
        /// Pauses the currently played song.
        /// </summary>
        public ReactiveUI.Legacy.ReactiveCommand PauseCommand { get; private set; }

        /// <summary>
        /// A command that decides whether the songs should be paused or continued.
        /// </summary>
        public ReactiveUI.Legacy.ReactiveCommand PauseContinueCommand { get; private set; }

        /// <summary>
        /// Plays the song that is currently selected in the playlist or continues the song if it is paused.
        /// </summary>
        public ReactiveUI.Legacy.ReactiveCommand PlayCommand { get; private set; }

        public IReactiveDerivedList<PlaylistViewModel> Playlists { get; private set; }

        /// <summary>
        /// Overrides the currently played song.
        /// </summary>
        public ReactiveUI.Legacy.ReactiveCommand PlayOverrideCommand { get; private set; }

        /// <summary>
        /// Plays the song that is before the currently played song in the playlist.
        /// </summary>
        public ReactiveUI.Legacy.ReactiveCommand PreviousSongCommand { get; private set; }

        public TimeSpan RemainingPlaylistTimeout
        {
            get { return this.library.RemainingPlaylistTimeout; }
        }

        public ReactiveUI.Legacy.ReactiveCommand RemovePlaylistCommand { get; private set; }

        public ReactiveUI.Legacy.ReactiveCommand RemoveSelectedPlaylistEntriesCommand { get; private set; }

        public IEnumerable<PlaylistEntryViewModel> SelectedPlaylistEntries
        {
            get { return this.selectedPlaylistEntries; }
            set { this.RaiseAndSetIfChanged(ref this.selectedPlaylistEntries, value ?? Enumerable.Empty<PlaylistEntryViewModel>()); }
        }

        public SettingsViewModel SettingsViewModel { get; private set; }

        public bool ShowPlaylistTimeout
        {
            get { return this.showPlaylistTimeout.Value; }
        }

        public ReactiveUI.Legacy.ReactiveCommand ShowSettingsCommand { get; private set; }

        public bool ShowVideoPlayer
        {
            get { return this.showVideoPlayer; }
            set { this.RaiseAndSetIfChanged(ref this.showVideoPlayer, value); }
        }

        public bool ShowVotes
        {
            get { return this.showVotes.Value; }
        }

        public ReactiveUI.Legacy.ReactiveCommand ShufflePlaylistCommand { get; private set; }

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
        public ReactiveUI.Legacy.ReactiveCommand UnMuteCommand { get; private set; }

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

        /// <summary>
        /// Creates a boolean observable that returns whether the user has the permission to do an
        /// administrator action.
        /// </summary>
        /// <param name="combinator">
        /// An additional combinator that returns whether the action is restricted in guest mode.
        /// </param>
        /// <remarks>
        /// The user has admin access =&gt; Always returns true The user has guest access and
        /// combinator is true =&gt; Returns false The user has guest access and combinator is false
        /// = &gt; Returns true
        /// </remarks>
        private IObservable<bool> HasAccess(IObservable<bool> combinator)
        {
            return this.library.LocalAccessControl.ObserveAccessPermission(this.accessToken)
                .Select(x => x == AccessPermission.Admin)
                .CombineLatest(combinator, (admin, c) => admin || !c);
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