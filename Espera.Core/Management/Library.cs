using Akavache;
using Espera.Core.Analytics;
using Espera.Core.Audio;
using Espera.Core.Settings;
using Rareform.Extensions;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;
using Splat;
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

            this.globalSubscriptions = new CompositeDisposable();
            this.accessControl = new AccessControl(settings);
            this.songLock = new ReaderWriterLockSlim();
            this.songs = new HashSet<LocalSong>();
            this.playlists = new ReactiveList<Playlist>();
            this.songsUpdated = new Subject<Unit>();
            this.audioPlayer = new AudioPlayer();
            this.manualUpdateTrigger = new Subject<Unit>();

            this.LoadedSong = this.audioPlayer.LoadedSong;
            this.TotalTime = this.audioPlayer.TotalTime;
            this.PlaybackState = this.audioPlayer.PlaybackState;

            this.WhenAnyValue(x => x.CurrentPlaylist.CanPlayNextSong).SampleAndCombineLatest(this.audioPlayer.PlaybackState
                    .Where(p => p == AudioPlayerState.Finished), (canPlayNextSong, _) => canPlayNextSong)
                .SelectMany(x => this.HandleSongFinishAsync(x).ToObservable())
                .Subscribe();

            this.CurrentPlaybackTime = this.audioPlayer.CurrentTimeChanged;

            this.volume = this.settings.WhenAnyValue(x => x.Volume)
                .ToProperty(this, x => x.Volume);
        }

        public IObservable<TimeSpan> CurrentPlaybackTime { get; private set; }

        public Playlist CurrentPlaylist
        {
            get { return this.currentPlaylist; }
            set { this.RaiseAndSetIfChanged(ref this.currentPlaylist, value); }
        }

        /// <summary>
        /// Gets a property that reports whether the library is currently looking for new songs at
        /// the song source or removing songs that don't exist anymore.
        /// </summary>
        public bool IsUpdating
        {
            get { return this.isUpdating; }
            private set { this.RaiseAndSetIfChanged(ref this.isUpdating, value); }
        }

        /// <summary>
        /// Gets the song that is currently loaded.
        /// </summary>
        public IObservable<Song> LoadedSong { get; private set; }

        public ILocalAccessControl LocalAccessControl
        {
            get { return this.accessControl; }
        }

        public IObservable<AudioPlayerState> PlaybackState { get; private set; }

        /// <summary>
        /// Returns an enumeration of playlists that implements <see cref="INotifyCollectionChanged"
        /// /> .
        /// </summary>
        public IReadOnlyReactiveList<Playlist> Playlists
        {
            get { return this.playlists; }
        }

        public IRemoteAccessControl RemoteAccessControl
        {
            get { return this.accessControl; }
        }

        /// <summary>
        /// Gets all songs that are currently in the library.
        /// </summary>
        public IReadOnlyList<LocalSong> Songs
        {
            get
            {
                this.songLock.EnterReadLock();

                List<LocalSong> tempSongs = this.songs.ToList();

                this.songLock.ExitReadLock();

                return tempSongs;
            }
        }

        public string SongSourcePath
        {
            get { return this.songSourcePath; }
            private set { this.RaiseAndSetIfChanged(ref this.songSourcePath, value); }
        }

        /// <summary>
        /// Occurs when a song has been added to the library.
        /// </summary>
        public IObservable<Unit> SongsUpdated
        {
            get { return this.songsUpdated.AsObservable(); }
        }

        /// <summary>
        /// Gets the duration of the current song.
        /// </summary>
        public IObservable<TimeSpan> TotalTime { get; private set; }

        public float Volume
        {
            get { return this.volume.Value; }
        }

        /// <summary>
        /// Adds a new playlist to the library and immediately sets it as the current playlist.
        /// </summary>
        /// <param name="name">
        /// The name of the playlist, It is required that no other playlist has this name.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// A playlist with the specified name already exists.
        /// </exception>
        public void AddAndSwitchToPlaylist(string name, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlaylist);

            this.AddPlaylist(name, accessToken);
            this.SwitchToPlaylist(this.GetPlaylistByName(name), accessToken);
        }

        /// <summary>
        /// Adds the specified song to the end of the playlist as guest.
        /// </summary>
        /// <remarks>
        /// <para>This method is intended only for guest access tokens.</para>
        /// <para>
        /// As soon as the song is added to the playlist, the entry is marked as "shadow voted".
        /// This means that it won't be favoured over other songs, like a regular vote, but stays at
        /// the end of the playlist.
        /// </para>
        /// <para>
        /// Shadow votes still decrease the available votes of the guest like regular votes, this
        /// prevents guests from spamming songs to the playlist.
        /// </para>
        /// </remarks>
        /// <param name="song">The song to add to the end of the playlist.</param>
        /// <param name="accessToken">The access token. Must have guest permission.</param>
        /// <exception cref="InvalidOperationException">The guest system is disabled.</exception>
        /// <exception cref="AccessException">The access token isn't a guest token.</exception>
        public void AddGuestSongToPlaylist(Song song, Guid accessToken)
        {
            if (song == null)
                throw new ArgumentNullException("song");

            this.accessControl.VerifyVotingPreconditions(accessToken);

            PlaylistEntry entry = this.CurrentPlaylist.AddShadowVotedSong(song);

            this.accessControl.RegisterShadowVote(accessToken, entry);
        }

        /// <summary>
        /// Adds a new playlist with the specified name to the library.
        /// </summary>
        /// <param name="name">
        /// The name of the playlist. It is required that no other playlist has this name.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// A playlist with the specified name already exists.
        /// </exception>
        public void AddPlaylist(string name, Guid accessToken)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (this.GetPlaylistByName(name) != null)
                throw new InvalidOperationException("A playlist with this name already exists.");

            this.accessControl.VerifyAccess(accessToken);

            this.playlists.Add(new Playlist(name));
        }

        /// <summary>
        /// Adds the specified song to the end of the playlist. This method is only available in
        /// administrator mode.
        /// </summary>
        /// <param name="songList">The songs to add to the end of the playlist.</param>
        public void AddSongsToPlaylist(IEnumerable<Song> songList, Guid accessToken)
        {
            if (songList == null)
                throw new ArgumentNullException("songList");

            this.accessControl.VerifyAccess(accessToken);

            this.CurrentPlaylist.AddSongs(songList.ToList()); // Copy the sequence to a list, so that the enumeration doesn't gets modified
        }

        public void ChangeSongSourcePath(string path, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken);

            if (!this.fileSystem.Directory.Exists(path))
                throw new InvalidOperationException("Directory does't exist.");

            this.SongSourcePath = path;
            this.UpdateNow();
        }

        /// <summary>
        /// Continues the currently loaded song.
        /// </summary>
        public async Task ContinueSongAsync(Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken);

            await this.audioPlayer.PlayAsync();
        }

        public void Dispose()
        {
            if (this.currentSongFinderSubscription != null)
            {
                this.currentSongFinderSubscription.Dispose();
            }

            this.globalSubscriptions.Dispose();
        }

        public Playlist GetPlaylistByName(string playlistName)
        {
            if (playlistName == null)
                Throw.ArgumentNullException(() => playlistName);

            return this.playlists.FirstOrDefault(playlist => playlist.Name == playlistName);
        }

        public void Initialize()
        {
            if (this.libraryReader.LibraryExists)
            {
                this.Load();
            }

            IObservable<Unit> updateTrigger = Observable.Return(Unit.Default)
                // Delay the initial library update so we don't thrash the CPU and disk when the
                // user is doing the important things
                .Delay(InitialUpdateDelay, RxApp.TaskpoolScheduler)
                .Concat(this.settings.WhenAnyValue(x => x.SongSourceUpdateInterval)
                    .Select(x => Observable.Interval(x, RxApp.TaskpoolScheduler))
                    .Switch()
                    .Select(_ => Unit.Default))
                .Where(_ => this.settings.EnableAutomaticLibraryUpdates)
                .Merge(this.manualUpdateTrigger);

            updateTrigger.Select(_ => this.SongSourcePath)
                .Where(path => !String.IsNullOrEmpty(path))
                .Do(_ => this.Log().Info("Triggering library update."))
                // Abort the update if the song source doesn't exist.
                // 
                // The source may be a NAS that's just temporarily offline and we don't want to
                // purge the whole library in this case.
                .Where(path =>
                {
                    bool exists = this.fileSystem.Directory.Exists(path);

                    if (!exists)
                    {
                        this.Log().Info("Song source isn't available, aborting library update.");
                    }

                    return exists;
                })
                .Subscribe(path => this.UpdateSongsAsync(path))
                .DisposeWith(this.globalSubscriptions);
        }

        public void MovePlaylistSong(int fromIndex, int toIndex, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlaylist);

            this.CurrentPlaylist.MoveSong(fromIndex, toIndex);
        }

        /// <summary>
        /// Pauses the currently loaded song.
        /// </summary>
        public async Task PauseSongAsync(Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlayPause);

            await this.audioPlayer.PauseAsync();
        }

        public async Task PlayInstantlyAsync(IEnumerable<Song> songList, Guid accessToken)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlayPause);

            Playlist existingTemporaryPlaylist = this.playlists.FirstOrDefault(x => x.IsTemporary);

            if (existingTemporaryPlaylist != null)
            {
                this.playlists.Remove(existingTemporaryPlaylist);
            }

            string instantPlaylistName = Guid.NewGuid().ToString();
            var temporaryPlaylist = new Playlist(instantPlaylistName, true);
            temporaryPlaylist.AddSongs(songList.ToList());

            this.playlists.Add(temporaryPlaylist);
            this.SwitchToPlaylist(temporaryPlaylist, accessToken);

            await this.PlaySongAsync(0, accessToken);
        }

        /// <summary>
        /// Plays the next song in the playlist.
        /// </summary>
        public async Task PlayNextSongAsync(Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken);

            await this.InternPlayNextSongAsync();
        }

        /// <summary>
        /// Plays the previous song in the playlist.
        /// </summary>
        public async Task PlayPreviousSongAsync(Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken, this.CurrentPlaylist.CanPlayPreviousSong);

            if (!this.CurrentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The previous song can't be played as there is no current playlist index.");

            await this.PlaySongAsync(this.CurrentPlaylist.CurrentSongIndex.Value - 1, accessToken);
        }

        /// <summary>
        /// Plays the song with the specified index in the playlist.
        /// </summary>
        /// <param name="playlistIndex">The index of the song in the playlist.</param>
        public async Task PlaySongAsync(int playlistIndex, Guid accessToken)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlayPause);

            await this.InternPlaySongAsync(playlistIndex);
        }

        public void RegisterAudioPlayerCallback(IMediaPlayerCallback audioPlayerCallback, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken);

            this.audioPlayer.RegisterAudioPlayerCallback(audioPlayerCallback);
        }

        public void RegisterVideoPlayerCallback(IMediaPlayerCallback videoPlayerCallback, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken);

            this.audioPlayer.RegisterVideoPlayerCallback(videoPlayerCallback);
        }

        /// <summary>
        /// Removes the songs with the specified indexes from the playlist.
        /// </summary>
        /// <param name="indexes">The indexes of the songs to remove from the playlist.</param>
        public void RemoveFromPlaylist(IEnumerable<int> indexes, Guid accessToken)
        {
            if (indexes == null)
                Throw.ArgumentNullException(() => indexes);

            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlaylist);

            this.RemoveFromPlaylist(this.CurrentPlaylist, indexes);
        }

        /// <summary>
        /// Removes the specified songs from the playlist.
        /// </summary>
        /// <param name="songList">The songs to remove.</param>
        public void RemoveFromPlaylist(IEnumerable<Song> songList, Guid accessToken)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            this.accessControl.VerifyAccess(accessToken);

            this.RemoveFromPlaylist(this.CurrentPlaylist, songList);
        }

        /// <summary>
        /// Removes the playlist with the specified name from the library.
        /// </summary>
        /// <param name="playlist">The playlist to remove.</param>
        public void RemovePlaylist(Playlist playlist, Guid accessToken)
        {
            if (playlist == null)
                Throw.ArgumentNullException(() => playlist);

            this.accessControl.VerifyAccess(accessToken);

            this.playlists.Remove(playlist);
        }

        /// <summary>
        /// Saves the library to the writer that was specified in the constructor. This method
        /// doesn't throw, even if there was an error when writing the library.
        /// </summary>
        public void Save()
        {
            var stopWatch = Stopwatch.StartNew();

            IReadOnlyList<LocalSong> songsToSave = this.Songs;
            IReadOnlyList<Playlist> playlistsToSave = this.playlists.Where(playlist => !playlist.IsTemporary).ToList();
            string pathToSave = this.SongSourcePath;

            try
            {
                Utility.Retry(() => this.libraryWriter.Write(songsToSave, playlistsToSave, pathToSave));

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
            this.accessControl.VerifyAccess(accessToken, this.settings.LockTime);

            this.audioPlayer.CurrentTime = currentTime;
        }

        public void SetVolume(float volume, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken, this.settings.LockVolume);

            this.settings.Volume = volume;
            this.audioPlayer.SetVolume(volume);
        }

        public void ShufflePlaylist(Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlaylist);

            this.CurrentPlaylist.Shuffle();
        }

        public void SwitchToPlaylist(Playlist playlist, Guid accessToken)
        {
            if (playlist == null)
                Throw.ArgumentNullException(() => playlist);

            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlaylist);

            this.CurrentPlaylist = playlist;
        }

        /// <summary>
        /// Triggers an update of the library, so it searches the library path for new songs.
        /// </summary>
        public void UpdateNow()
        {
            this.manualUpdateTrigger.OnNext(Unit.Default);
        }

        public void VoteForPlaylistEntry(int index, Guid accessToken)
        {
            this.accessControl.VerifyVotingPreconditions(accessToken);

            PlaylistEntry entry = this.CurrentPlaylist.VoteFor(index);

            this.accessControl.RegisterVote(accessToken, entry);
        }

        private async Task HandleSongCorruptionAsync()
        {
            if (!this.CurrentPlaylist.CanPlayNextSong)
            {
                this.CurrentPlaylist.CurrentSongIndex = null;
            }

            else
            {
                await this.InternPlayNextSongAsync();
            }
        }

        private async Task HandleSongFinishAsync(bool canPlayNextSong)
        {
            if (!canPlayNextSong)
            {
                this.CurrentPlaylist.CurrentSongIndex = null;
            }

            if (canPlayNextSong)
            {
                await this.InternPlayNextSongAsync();
            }
        }

        private Task InternPlayNextSongAsync()
        {
            if (!this.CurrentPlaylist.CanPlayNextSong || !this.CurrentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            int nextIndex = this.CurrentPlaylist.CurrentSongIndex.Value + 1;

            return this.InternPlaySongAsync(nextIndex);
        }

        private async Task InternPlaySongAsync(int playlistIndex)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            if (this.currentPlayingPlaylist != null && this.currentPlayingPlaylist != this.CurrentPlaylist)
            {
                this.currentPlayingPlaylist.CurrentSongIndex = null;
            }

            this.currentPlayingPlaylist = this.CurrentPlaylist;

            this.CurrentPlaylist.CurrentSongIndex = playlistIndex;

            Song song = this.CurrentPlaylist[playlistIndex].Song;

            try
            {
                await song.PrepareAsync(this.settings.StreamHighestYoutubeQuality ? YoutubeStreamingQuality.High : this.settings.YoutubeStreamingQuality)
                    .ToObservable().Timeout(Library.PreparationTimeout, RxApp.TaskpoolScheduler);
            }

            catch (SongPreparationException ex)
            {
                this.Log().ErrorException("Failed to prepare song", ex);

                this.HandleSongCorruptionAsync();

                return;
            }

            catch (TimeoutException ex)
            {
                this.Log().ErrorException("Song preparation timeout", ex);

                this.HandleSongCorruptionAsync();

                return;
            }

            try
            {
                await this.audioPlayer.LoadAsync(song);
            }

            catch (SongLoadException ex)
            {
                this.Log().ErrorException("Failed to load song", ex);
                song.IsCorrupted = true;

                this.HandleSongCorruptionAsync();

                return;
            }

            this.audioPlayer.SetVolume(this.Volume);

            try
            {
                await this.audioPlayer.PlayAsync();
            }

            catch (PlaybackException ex)
            {
                this.Log().ErrorException("Failed to play song", ex);
                song.IsCorrupted = true;

                this.HandleSongCorruptionAsync();

                return;
            }

            song.IsCorrupted = false;
        }

        /// <summary>
        /// Loads the library with the reader specified in the constructor. This methods retries
        /// three times, but doesn't throw if the loading failed after that.
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
                        savedSongs = this.libraryReader.ReadSongs();
                        savedPlaylists = this.libraryReader.ReadPlaylists();
                        songsPath = this.libraryReader.ReadSongSourcePath();
                    }

                    finally
                    {
                        this.libraryReader.InvalidateCache();
                    }
                });
            }

            catch (LibraryReadException ex)
            {
                AnalyticsClient.Instance.RecordNonFatalError(ex);
                this.Log().FatalException("Failed to load the library file.", ex);

                return;
            }

            foreach (LocalSong song in savedSongs)
            {
                this.songs.Add(song);
            }

            foreach (Playlist playlist in savedPlaylists)
            {
                this.playlists.Add(playlist);
            }

            this.SongSourcePath = songsPath;

            stopWatch.Stop();
            this.Log().Info("Library load took {0}ms", stopWatch.ElapsedMilliseconds);
        }

        private async Task RemoveFromLibrary(IEnumerable<LocalSong> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            List<LocalSong> enumerable = songList.ToList();

            // Check if the number of occurences of the artwork key match the number of songs with
            // the same artwork key so we don't delete artwork keys that still have a corresponding
            // song in the library
            Dictionary<string, int> artworkKeys = this.Songs
                .Select(x => x.ArtworkKey)
                .Where(x => x != null)
                .GroupBy(x => x)
                .ToDictionary(x => x.Key, x => x.Count());

            var artworkKeysToDelete = enumerable
                .GroupBy(x => x.ArtworkKey)
                .Where(x => x != null && x.Key != null && artworkKeys[x.Key] == x.Count())
                .Select(x => x.Key)
                .ToList();

            if (artworkKeysToDelete.Any())
            {
                await BlobCache.LocalMachine.Invalidate(artworkKeysToDelete);
            }

            this.playlists.ForEach(playlist => this.RemoveFromPlaylist(playlist, enumerable));

            this.songLock.EnterWriteLock();

            foreach (LocalSong song in enumerable)
            {
                this.songs.Remove(song);
            }

            this.songLock.ExitWriteLock();
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<int> indexes)
        {
            bool stopCurrentSong = playlist == this.CurrentPlaylist && indexes.Any(index => index == this.CurrentPlaylist.CurrentSongIndex);

            playlist.RemoveSongs(indexes);

            if (stopCurrentSong)
            {
                this.audioPlayer.StopAsync();
            }
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<Song> songList)
        {
            this.RemoveFromPlaylist(playlist, playlist.GetIndexes(songList));
        }

        private async Task RemoveMissingSongsAsync(string currentPath)
        {
            List<LocalSong> currentSongs = this.Songs.ToList();

            List<LocalSong> notInAnySongSource = currentSongs
                .Where(song => !song.OriginalPath.StartsWith(currentPath))
                .ToList();

            HashSet<LocalSong> removable = null;

            await Task.Run(() =>
            {
                List<LocalSong> nonExistant = currentSongs
                    .Where(song => !this.fileSystem.File.Exists(song.OriginalPath))
                    .ToList();

                removable = new HashSet<LocalSong>(notInAnySongSource.Concat(nonExistant));
            });

            await this.RemoveFromLibrary(removable);

            if (removable.Any())
            {
                this.songsUpdated.OnNext(Unit.Default);
            }
        }

        private async Task StartOnlineArtworkLookup()
        {
            this.Log().Info("Starting online artwork lookup");

            List<LocalSong> songsWithoutArtwork = this.Songs.Where(x => x.ArtworkKey == null).ToList();

            this.Log().Info("{0} songs don't have an artwork", songsWithoutArtwork.Count);

            foreach (LocalSong song in songsWithoutArtwork)
            {
                string key = null;

                try
                {
                    key = await ArtworkCache.Instance.FetchOnline(song.Artist, song.Album);
                }

                catch (ArtworkCacheException ex)
                {
                    this.Log().ErrorException(String.Format("Error while fetching artwork for {0} - {1}", song.Artist, song.Album), ex);
                }

                if (key != null)
                {
                    song.ArtworkKey = key;
                }
            }

            this.Log().Info("Finished online artwork lookup");
        }

        private async Task UpdateSongsAsync(string path)
        {
            if (this.currentSongFinderSubscription != null)
            {
                this.currentSongFinderSubscription.Dispose();
                this.currentSongFinderSubscription = null;
            }

            this.IsUpdating = true;

            await this.RemoveMissingSongsAsync(path);

            ILocalSongFinder songFinder = this.localSongFinderFunc(path);

            this.currentSongFinderSubscription = songFinder.GetSongsAsync()
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(t =>
                {
                    LocalSong song = t.Item1;

                    this.songLock.EnterWriteLock();

                    bool added = this.songs.Add(song);

                    LocalSong realSong;
                    bool needsUpdate = false;

                    if (added)
                    {
                        realSong = song;
                        needsUpdate = true;
                    }

                    else
                    {
                        LocalSong existing = this.songs.First(x => x.OriginalPath == song.OriginalPath);

                        if (existing.UpdateMetadataFrom(song))
                        {
                            needsUpdate = true;
                        }

                        realSong = existing;
                    }

                    this.songLock.ExitWriteLock();

                    byte[] artworkData = t.Item2;

                    if (artworkData != null)
                    {
                        string key = BlobCacheKeys.GetKeyForArtwork(artworkData);

                        if (realSong.ArtworkKey != key)
                        {
                            ArtworkCache.Instance.Store(key, artworkData).ToObservable()
                                .Subscribe(x => realSong.ArtworkKey = key);
                        }
                    }

                    if (needsUpdate)
                    {
                        this.songsUpdated.OnNext(Unit.Default);
                    }
                }, () =>
                {
                    this.Save();

                    this.StartOnlineArtworkLookup();

                    this.songLock.EnterReadLock();
                    int songCount = this.songs.Count;
                    this.songLock.ExitReadLock();

                    AnalyticsClient.Instance.RecordLibrarySize(songCount);

                    this.IsUpdating = false;
                });
        }
    }
}