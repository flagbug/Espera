using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Akavache;
using Espera.Core.Analytics;
using Espera.Core.Audio;
using Espera.Core.Settings;
using Rareform.Extensions;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;
using Splat;

namespace Espera.Core.Management
{
    public sealed class Library : ReactiveObject, IDisposable
    {
        public static readonly TimeSpan InitialUpdateDelay = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan PreparationTimeout = TimeSpan.FromSeconds(10);
        private readonly AccessControl accessControl;
        private readonly AudioPlayer audioPlayer;
        private readonly IFileSystem fileSystem;
        private readonly CompositeDisposable globalSubscriptions;
        private readonly ILibraryReader libraryReader;
        private readonly ILibraryWriter libraryWriter;
        private readonly Func<string, ILocalSongFinder> localSongFinderFunc;
        private readonly Subject<Unit> manualUpdateTrigger;
        private readonly ReactiveList<Playlist> playlists;
        private readonly CoreSettings settings;
        private readonly ReaderWriterLockSlim songLock;
        private readonly HashSet<LocalSong> songs;
        private readonly Subject<Unit> songsUpdated;
        private readonly ObservableAsPropertyHelper<float> volume;
        private Playlist currentPlayingPlaylist;
        private Playlist currentPlaylist;
        private IDisposable currentSongFinderSubscription;
        private bool isUpdating;

        private string songSourcePath;

        public Library(ILibraryReader libraryReader, ILibraryWriter libraryWriter, CoreSettings settings,
            IFileSystem fileSystem, Func<string, ILocalSongFinder> localSongFinderFunc = null)
        {
            if (libraryReader == null)
                throw new ArgumentNullException("libraryReader");

            if (libraryWriter == null)
                throw new ArgumentNullException("libraryWriter");

            if (settings == null)
                throw new ArgumentNullException("settings");

            if (fileSystem == null)
                throw new ArgumentNullException("fileSystem");

            this.libraryReader = libraryReader;
            this.libraryWriter = libraryWriter;
            this.settings = settings;
            this.fileSystem = fileSystem;
            this.localSongFinderFunc = localSongFinderFunc ?? (x => new LocalSongFinder(x));

            globalSubscriptions = new CompositeDisposable();
            accessControl = new AccessControl(settings);
            songLock = new ReaderWriterLockSlim();
            songs = new HashSet<LocalSong>();
            playlists = new ReactiveList<Playlist>();
            songsUpdated = new Subject<Unit>();
            audioPlayer = new AudioPlayer();
            manualUpdateTrigger = new Subject<Unit>();

            LoadedSong = audioPlayer.LoadedSong;
            TotalTime = audioPlayer.TotalTime;
            PlaybackState = audioPlayer.PlaybackState;

            this.WhenAnyValue(x => x.CurrentPlaylist.CanPlayNextSong).SampleAndCombineLatest(audioPlayer.PlaybackState
                    .Where(p => p == AudioPlayerState.Finished), (canPlayNextSong, _) => canPlayNextSong)
                .SelectMany(x => HandleSongFinishAsync(x).ToObservable())
                .Subscribe();

            CurrentPlaybackTime = audioPlayer.CurrentTimeChanged;

            volume = this.settings.WhenAnyValue(x => x.Volume)
                .ToProperty(this, x => x.Volume);
        }

        public IObservable<TimeSpan> CurrentPlaybackTime { get; }

        public Playlist CurrentPlaylist
        {
            get => currentPlaylist;
            set => this.RaiseAndSetIfChanged(ref currentPlaylist, value);
        }

        /// <summary>
        ///     Gets a property that reports whether the library is currently looking for new songs at
        ///     the song source or removing songs that don't exist anymore.
        /// </summary>
        public bool IsUpdating
        {
            get => isUpdating;
            private set => this.RaiseAndSetIfChanged(ref isUpdating, value);
        }

        /// <summary>
        ///     Gets the song that is currently loaded.
        /// </summary>
        public IObservable<Song> LoadedSong { get; }

        public ILocalAccessControl LocalAccessControl => accessControl;

        public IObservable<AudioPlayerState> PlaybackState { get; }

        /// <summary>
        ///     Returns an enumeration of playlists that implements <see cref="INotifyCollectionChanged" /> .
        /// </summary>
        public IReadOnlyReactiveList<Playlist> Playlists => playlists;

        public IRemoteAccessControl RemoteAccessControl => accessControl;

        /// <summary>
        ///     Gets all songs that are currently in the library.
        /// </summary>
        public IReadOnlyList<LocalSong> Songs
        {
            get
            {
                songLock.EnterReadLock();

                var tempSongs = songs.ToList();

                songLock.ExitReadLock();

                return tempSongs;
            }
        }

        public string SongSourcePath
        {
            get => songSourcePath;
            private set => this.RaiseAndSetIfChanged(ref songSourcePath, value);
        }

        /// <summary>
        ///     Occurs when a song has been added to the library.
        /// </summary>
        public IObservable<Unit> SongsUpdated => songsUpdated.AsObservable();

        /// <summary>
        ///     Gets the duration of the current song.
        /// </summary>
        public IObservable<TimeSpan> TotalTime { get; }

        public float Volume => volume.Value;

        public void Dispose()
        {
            if (currentSongFinderSubscription != null) currentSongFinderSubscription.Dispose();

            globalSubscriptions.Dispose();
        }

        /// <summary>
        ///     Adds a new playlist to the library and immediately sets it as the current playlist.
        /// </summary>
        /// <param name="name">
        ///     The name of the playlist, It is required that no other playlist has this name.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///     A playlist with the specified name already exists.
        /// </exception>
        public void AddAndSwitchToPlaylist(string name, Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken, settings.LockPlaylist);

            AddPlaylist(name, accessToken);
            SwitchToPlaylist(GetPlaylistByName(name), accessToken);
        }

        /// <summary>
        ///     Adds the specified song to the end of the playlist as guest.
        /// </summary>
        /// <remarks>
        ///     <para>This method is intended only for guest access tokens.</para>
        ///     <para>
        ///         As soon as the song is added to the playlist, the entry is marked as "shadow voted".
        ///         This means that it won't be favoured over other songs, like a regular vote, but stays at
        ///         the end of the playlist.
        ///     </para>
        ///     <para>
        ///         Shadow votes still decrease the available votes of the guest like regular votes, this
        ///         prevents guests from spamming songs to the playlist.
        ///     </para>
        /// </remarks>
        /// <param name="song">The song to add to the end of the playlist.</param>
        /// <param name="accessToken">The access token. Must have guest permission.</param>
        /// <exception cref="InvalidOperationException">The guest system is disabled.</exception>
        /// <exception cref="AccessException">The access token isn't a guest token.</exception>
        public void AddGuestSongToPlaylist(Song song, Guid accessToken)
        {
            if (song == null)
                throw new ArgumentNullException("song");

            accessControl.VerifyVotingPreconditions(accessToken);

            var entry = CurrentPlaylist.AddShadowVotedSong(song);

            accessControl.RegisterShadowVote(accessToken, entry);
        }

        /// <summary>
        ///     Adds a new playlist with the specified name to the library.
        /// </summary>
        /// <param name="name">
        ///     The name of the playlist. It is required that no other playlist has this name.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///     A playlist with the specified name already exists.
        /// </exception>
        public void AddPlaylist(string name, Guid accessToken)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (GetPlaylistByName(name) != null)
                throw new InvalidOperationException("A playlist with this name already exists.");

            accessControl.VerifyAccess(accessToken);

            playlists.Add(new Playlist(name));
        }

        /// <summary>
        ///     Adds the specified song to the end of the playlist. This method is only available in
        ///     administrator mode.
        /// </summary>
        /// <param name="songList">The songs to add to the end of the playlist.</param>
        public void AddSongsToPlaylist(IEnumerable<Song> songList, Guid accessToken)
        {
            if (songList == null)
                throw new ArgumentNullException("songList");

            accessControl.VerifyAccess(accessToken);

            CurrentPlaylist.AddSongs(songList
                .ToList()); // Copy the sequence to a list, so that the enumeration doesn't gets modified
        }

        public void ChangeSongSourcePath(string path, Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken);

            if (!fileSystem.Directory.Exists(path))
                throw new InvalidOperationException("Directory does't exist.");

            SongSourcePath = path;
            UpdateNow();
        }

        /// <summary>
        ///     Continues the currently loaded song.
        /// </summary>
        public async Task ContinueSongAsync(Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken);

            await audioPlayer.PlayAsync();
        }

        public Playlist GetPlaylistByName(string playlistName)
        {
            if (playlistName == null)
                Throw.ArgumentNullException(() => playlistName);

            return playlists.FirstOrDefault(playlist => playlist.Name == playlistName);
        }

        public void Initialize()
        {
            if (libraryReader.LibraryExists) Load();

            var updateTrigger = Observable.Return(Unit.Default)
                // Delay the initial library update so we don't thrash the CPU and disk when the
                // user is doing the important things
                .Delay(InitialUpdateDelay, RxApp.TaskpoolScheduler)
                .Concat(settings.WhenAnyValue(x => x.SongSourceUpdateInterval)
                    .Select(x => Observable.Interval(x, RxApp.TaskpoolScheduler))
                    .Switch()
                    .Select(_ => Unit.Default))
                .Where(_ => settings.EnableAutomaticLibraryUpdates)
                .Merge(manualUpdateTrigger);

            updateTrigger.Select(_ => SongSourcePath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Do(_ => this.Log().Info("Triggering library update."))
                // Abort the update if the song source doesn't exist.
                // 
                // The source may be a NAS that's just temporarily offline and we don't want to
                // purge the whole library in this case.
                .Where(path =>
                {
                    var exists = fileSystem.Directory.Exists(path);

                    if (!exists) this.Log().Info("Song source isn't available, aborting library update.");

                    return exists;
                })
                .Subscribe(path => UpdateSongsAsync(path))
                .DisposeWith(globalSubscriptions);
        }

        public void MovePlaylistSong(int fromIndex, int toIndex, Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken, settings.LockPlaylist);

            CurrentPlaylist.MoveSong(fromIndex, toIndex);
        }

        /// <summary>
        ///     Pauses the currently loaded song.
        /// </summary>
        public async Task PauseSongAsync(Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken, settings.LockPlayPause);

            await audioPlayer.PauseAsync();
        }

        public async Task PlayInstantlyAsync(IEnumerable<Song> songList, Guid accessToken)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            accessControl.VerifyAccess(accessToken, settings.LockPlayPause);

            var existingTemporaryPlaylist = playlists.FirstOrDefault(x => x.IsTemporary);

            if (existingTemporaryPlaylist != null) playlists.Remove(existingTemporaryPlaylist);

            var instantPlaylistName = Guid.NewGuid().ToString();
            var temporaryPlaylist = new Playlist(instantPlaylistName, true);
            temporaryPlaylist.AddSongs(songList.ToList());

            playlists.Add(temporaryPlaylist);
            SwitchToPlaylist(temporaryPlaylist, accessToken);

            await PlaySongAsync(0, accessToken);
        }

        /// <summary>
        ///     Plays the next song in the playlist.
        /// </summary>
        public async Task PlayNextSongAsync(Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken);

            await InternPlayNextSongAsync();
        }

        /// <summary>
        ///     Plays the previous song in the playlist.
        /// </summary>
        public async Task PlayPreviousSongAsync(Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken, CurrentPlaylist.CanPlayPreviousSong);

            if (!CurrentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException(
                    "The previous song can't be played as there is no current playlist index.");

            await PlaySongAsync(CurrentPlaylist.CurrentSongIndex.Value - 1, accessToken);
        }

        /// <summary>
        ///     Plays the song with the specified index in the playlist.
        /// </summary>
        /// <param name="playlistIndex">The index of the song in the playlist.</param>
        public async Task PlaySongAsync(int playlistIndex, Guid accessToken)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            accessControl.VerifyAccess(accessToken, settings.LockPlayPause);

            await InternPlaySongAsync(playlistIndex);
        }

        public void RegisterAudioPlayerCallback(IMediaPlayerCallback audioPlayerCallback, Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken);

            audioPlayer.RegisterAudioPlayerCallback(audioPlayerCallback);
        }

        public void RegisterVideoPlayerCallback(IMediaPlayerCallback videoPlayerCallback, Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken);

            audioPlayer.RegisterVideoPlayerCallback(videoPlayerCallback);
        }

        /// <summary>
        ///     Removes the songs with the specified indexes from the playlist.
        /// </summary>
        /// <param name="indexes">The indexes of the songs to remove from the playlist.</param>
        public void RemoveFromPlaylist(IEnumerable<int> indexes, Guid accessToken)
        {
            if (indexes == null)
                Throw.ArgumentNullException(() => indexes);

            accessControl.VerifyAccess(accessToken, settings.LockPlaylist);

            RemoveFromPlaylist(CurrentPlaylist, indexes);
        }

        /// <summary>
        ///     Removes the specified songs from the playlist.
        /// </summary>
        /// <param name="songList">The songs to remove.</param>
        public void RemoveFromPlaylist(IEnumerable<Song> songList, Guid accessToken)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            accessControl.VerifyAccess(accessToken);

            RemoveFromPlaylist(CurrentPlaylist, songList);
        }

        /// <summary>
        ///     Removes the playlist with the specified name from the library.
        /// </summary>
        /// <param name="playlist">The playlist to remove.</param>
        public void RemovePlaylist(Playlist playlist, Guid accessToken)
        {
            if (playlist == null)
                Throw.ArgumentNullException(() => playlist);

            accessControl.VerifyAccess(accessToken);

            playlists.Remove(playlist);
        }

        /// <summary>
        ///     Saves the library to the writer that was specified in the constructor. This method
        ///     doesn't throw, even if there was an error when writing the library.
        /// </summary>
        public void Save()
        {
            var stopWatch = Stopwatch.StartNew();

            var songsToSave = Songs;
            IReadOnlyList<Playlist> playlistsToSave = playlists.Where(playlist => !playlist.IsTemporary).ToList();
            var pathToSave = SongSourcePath;

            try
            {
                Utility.Retry(() => libraryWriter.Write(songsToSave, playlistsToSave, pathToSave));

                stopWatch.Stop();
                this.Log().Info("Library save took {0}ms", stopWatch.ElapsedMilliseconds);
            }

            catch (LibraryWriteException ex)
            {
                AnalyticsClient.Instance.RecordNonFatalError(ex);
                this.Log().FatalException("Couldn't write the library file", ex);
            }

            finally
            {
                stopWatch.Stop();
            }
        }

        public void SetCurrentTime(TimeSpan currentTime, Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken, settings.LockTime);

            audioPlayer.CurrentTime = currentTime;
        }

        public void SetVolume(float volume, Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken, settings.LockVolume);

            settings.Volume = volume;
            audioPlayer.SetVolume(volume);
        }

        public void ShufflePlaylist(Guid accessToken)
        {
            accessControl.VerifyAccess(accessToken, settings.LockPlaylist);

            CurrentPlaylist.Shuffle();
        }

        public void SwitchToPlaylist(Playlist playlist, Guid accessToken)
        {
            if (playlist == null)
                Throw.ArgumentNullException(() => playlist);

            accessControl.VerifyAccess(accessToken, settings.LockPlaylist);

            CurrentPlaylist = playlist;
        }

        /// <summary>
        ///     Triggers an update of the library, so it searches the library path for new songs.
        /// </summary>
        public void UpdateNow()
        {
            manualUpdateTrigger.OnNext(Unit.Default);
        }

        public void VoteForPlaylistEntry(int index, Guid accessToken)
        {
            accessControl.VerifyVotingPreconditions(accessToken);

            var entry = CurrentPlaylist.VoteFor(index);

            accessControl.RegisterVote(accessToken, entry);
        }

        private async Task HandleSongCorruptionAsync()
        {
            if (!CurrentPlaylist.CanPlayNextSong)
                CurrentPlaylist.CurrentSongIndex = null;

            else
                await InternPlayNextSongAsync();
        }

        private async Task HandleSongFinishAsync(bool canPlayNextSong)
        {
            if (!canPlayNextSong) CurrentPlaylist.CurrentSongIndex = null;

            if (canPlayNextSong) await InternPlayNextSongAsync();
        }

        private Task InternPlayNextSongAsync()
        {
            if (!CurrentPlaylist.CanPlayNextSong || !CurrentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            var nextIndex = CurrentPlaylist.CurrentSongIndex.Value + 1;

            return InternPlaySongAsync(nextIndex);
        }

        private async Task InternPlaySongAsync(int playlistIndex)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            if (currentPlayingPlaylist != null && currentPlayingPlaylist != CurrentPlaylist)
                currentPlayingPlaylist.CurrentSongIndex = null;

            currentPlayingPlaylist = CurrentPlaylist;

            CurrentPlaylist.CurrentSongIndex = playlistIndex;

            var song = CurrentPlaylist[playlistIndex].Song;

            try
            {
                await song.PrepareAsync(settings.StreamHighestYoutubeQuality
                        ? YoutubeStreamingQuality.High
                        : settings.YoutubeStreamingQuality)
                    .ToObservable().Timeout(PreparationTimeout, RxApp.TaskpoolScheduler);
            }

            catch (SongPreparationException ex)
            {
                this.Log().ErrorException("Failed to prepare song", ex);

                HandleSongCorruptionAsync();

                return;
            }

            catch (TimeoutException ex)
            {
                this.Log().ErrorException("Song preparation timeout", ex);

                HandleSongCorruptionAsync();

                return;
            }

            try
            {
                await audioPlayer.LoadAsync(song);
            }

            catch (SongLoadException ex)
            {
                this.Log().ErrorException("Failed to load song", ex);
                song.IsCorrupted = true;

                HandleSongCorruptionAsync();

                return;
            }

            audioPlayer.SetVolume(Volume);

            try
            {
                await audioPlayer.PlayAsync();
            }

            catch (PlaybackException ex)
            {
                this.Log().ErrorException("Failed to play song", ex);
                song.IsCorrupted = true;

                HandleSongCorruptionAsync();

                return;
            }

            song.IsCorrupted = false;
        }

        /// <summary>
        ///     Loads the library with the reader specified in the constructor. This methods retries
        ///     three times, but doesn't throw if the loading failed after that.
        /// </summary>
        private void Load()
        {
            var stopWatch = Stopwatch.StartNew();

            IEnumerable<LocalSong> savedSongs = null;
            IEnumerable<Playlist> savedPlaylists = null;
            string songsPath = null;

            try
            {
                Utility.Retry(() =>
                {
                    try
                    {
                        savedSongs = libraryReader.ReadSongs();
                        savedPlaylists = libraryReader.ReadPlaylists();
                        songsPath = libraryReader.ReadSongSourcePath();
                    }

                    finally
                    {
                        libraryReader.InvalidateCache();
                    }
                });
            }

            catch (LibraryReadException ex)
            {
                AnalyticsClient.Instance.RecordNonFatalError(ex);
                this.Log().FatalException("Failed to load the library file.", ex);

                return;
            }

            foreach (var song in savedSongs) songs.Add(song);

            foreach (var playlist in savedPlaylists) playlists.Add(playlist);

            SongSourcePath = songsPath;

            stopWatch.Stop();
            this.Log().Info("Library load took {0}ms", stopWatch.ElapsedMilliseconds);
        }

        private async Task RemoveFromLibrary(IEnumerable<LocalSong> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            var enumerable = songList.ToList();

            // Check if the number of occurences of the artwork key match the number of songs with
            // the same artwork key so we don't delete artwork keys that still have a corresponding
            // song in the library
            var artworkKeys = Songs
                .Select(x => x.ArtworkKey)
                .Where(x => x != null)
                .GroupBy(x => x)
                .ToDictionary(x => x.Key, x => x.Count());

            var artworkKeysToDelete = enumerable
                .GroupBy(x => x.ArtworkKey)
                .Where(x => x != null && x.Key != null && artworkKeys[x.Key] == x.Count())
                .Select(x => x.Key)
                .ToList();

            if (artworkKeysToDelete.Any()) await BlobCache.LocalMachine.Invalidate(artworkKeysToDelete);

            playlists.ForEach(playlist => RemoveFromPlaylist(playlist, enumerable));

            songLock.EnterWriteLock();

            foreach (var song in enumerable) songs.Remove(song);

            songLock.ExitWriteLock();
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<int> indexes)
        {
            var stopCurrentSong = playlist == CurrentPlaylist &&
                                  indexes.Any(index => index == CurrentPlaylist.CurrentSongIndex);

            playlist.RemoveSongs(indexes);

            if (stopCurrentSong) audioPlayer.StopAsync();
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<Song> songList)
        {
            RemoveFromPlaylist(playlist, playlist.GetIndexes(songList));
        }

        private async Task RemoveMissingSongsAsync(string currentPath)
        {
            var currentSongs = Songs.ToList();

            var notInAnySongSource = currentSongs
                .Where(song => !song.OriginalPath.StartsWith(currentPath))
                .ToList();

            HashSet<LocalSong> removable = null;

            await Task.Run(() =>
            {
                var nonExistant = currentSongs
                    .Where(song => !fileSystem.File.Exists(song.OriginalPath))
                    .ToList();

                removable = new HashSet<LocalSong>(notInAnySongSource.Concat(nonExistant));
            });

            await RemoveFromLibrary(removable);

            if (removable.Any()) songsUpdated.OnNext(Unit.Default);
        }

        private async Task StartOnlineArtworkLookup()
        {
            this.Log().Info("Starting online artwork lookup");

            var songsWithoutArtwork = Songs.Where(x => x.ArtworkKey == null).ToList();

            this.Log().Info("{0} songs don't have an artwork", songsWithoutArtwork.Count);

            foreach (var song in songsWithoutArtwork)
            {
                string key = null;

                try
                {
                    key = await ArtworkCache.Instance.FetchOnline(song.Artist, song.Album);
                }

                catch (ArtworkCacheException ex)
                {
                    this.Log().ErrorException(
                        string.Format("Error while fetching artwork for {0} - {1}", song.Artist, song.Album), ex);
                }

                if (key != null) song.ArtworkKey = key;
            }

            this.Log().Info("Finished online artwork lookup");
        }

        private async Task UpdateSongsAsync(string path)
        {
            if (currentSongFinderSubscription != null)
            {
                currentSongFinderSubscription.Dispose();
                currentSongFinderSubscription = null;
            }

            IsUpdating = true;

            await RemoveMissingSongsAsync(path);

            var songFinder = localSongFinderFunc(path);

            currentSongFinderSubscription = songFinder.GetSongsAsync()
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(t =>
                {
                    var song = t.Item1;

                    songLock.EnterWriteLock();

                    var added = songs.Add(song);

                    LocalSong realSong;
                    var needsUpdate = false;

                    if (added)
                    {
                        realSong = song;
                        needsUpdate = true;
                    }

                    else
                    {
                        var existing = songs.First(x => x.OriginalPath == song.OriginalPath);

                        if (existing.UpdateMetadataFrom(song)) needsUpdate = true;

                        realSong = existing;
                    }

                    songLock.ExitWriteLock();

                    var artworkData = t.Item2;

                    if (artworkData != null)
                    {
                        var key = BlobCacheKeys.GetKeyForArtwork(artworkData);

                        if (realSong.ArtworkKey != key)
                            ArtworkCache.Instance.Store(key, artworkData).ToObservable()
                                .Subscribe(x => realSong.ArtworkKey = key);
                    }

                    if (needsUpdate) songsUpdated.OnNext(Unit.Default);
                }, () =>
                {
                    Save();

                    StartOnlineArtworkLookup();

                    songLock.EnterReadLock();
                    var songCount = songs.Count;
                    songLock.ExitReadLock();

                    AnalyticsClient.Instance.RecordLibrarySize(songCount);

                    IsUpdating = false;
                });
        }
    }
}