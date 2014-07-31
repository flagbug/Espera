using Akavache;
using Espera.Core.Analytics;
using Espera.Core.Audio;
using Espera.Core.Settings;
using Rareform.Extensions;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;
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
        public static readonly TimeSpan PreparationTimeout = TimeSpan.FromSeconds(10);

        private readonly AccessControl accessControl;
        private readonly AudioPlayer audioPlayer;
        private readonly Subject<Playlist> currentPlaylistChanged;
        private readonly IFileSystem fileSystem;
        private readonly CompositeDisposable globalSubscriptions;
        private readonly BehaviorSubject<bool> isUpdating;
        private readonly ILibraryReader libraryReader;
        private readonly ILibraryWriter libraryWriter;
        private readonly Func<string, ILocalSongFinder> localSongFinderFunc;
        private readonly Subject<Unit> manualUpdateTrigger;
        private readonly ReactiveList<Playlist> playlists;
        private readonly CoreSettings settings;
        private readonly ReaderWriterLockSlim songLock;
        private readonly HashSet<LocalSong> songs;
        private readonly BehaviorSubject<string> songSourcePath;
        private readonly Subject<Unit> songsUpdated;
        private readonly ObservableAsPropertyHelper<float> volume;
        private Playlist currentPlayingPlaylist;
        private IDisposable currentSongFinderSubscription;
        private DateTime lastSongAddTime;

        public Library(ILibraryReader libraryReader, ILibraryWriter libraryWriter, CoreSettings settings,
            IFileSystem fileSystem, Func<string, ILocalSongFinder> localSongFinderFunc = null)
        {
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
            this.currentPlaylistChanged = new Subject<Playlist>();
            this.CanPlayNextSong = this.currentPlaylistChanged.Select(x => x.CanPlayNextSong).Switch();
            this.CanPlayPreviousSong = this.currentPlaylistChanged.Select(x => x.CanPlayPreviousSong).Switch();
            this.songSourcePath = new BehaviorSubject<string>(null);
            this.songsUpdated = new Subject<Unit>();
            this.audioPlayer = new AudioPlayer();
            this.manualUpdateTrigger = new Subject<Unit>();
            this.isUpdating = new BehaviorSubject<bool>(false);

            this.LoadedSong = this.audioPlayer.LoadedSong;
            this.TotalTime = this.audioPlayer.TotalTime;
            this.PlaybackState = this.audioPlayer.PlaybackState;

            this.CanPlayNextSong.SampleAndCombineLatest(this.audioPlayer.PlaybackState
                    .Where(p => p == AudioPlayerState.Finished), (canPlayNextSong, _) => canPlayNextSong)
                .SelectMany(x => this.HandleSongFinishAsync(x).ToObservable())
                .Subscribe();

            this.CurrentPlaybackTime = this.audioPlayer.CurrentTimeChanged;

            this.volume = this.settings.WhenAnyValue(x => x.Volume)
                .ToProperty(this, x => x.Volume);
        }

        /// <summary>
        /// Gets a value indicating whether the next song in the playlist can be played.
        /// </summary>
        /// <value>true if the next song in the playlist can be played; otherwise, false.</value>
        public IObservable<bool> CanPlayNextSong { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the previous song in the playlist can be played.
        /// </summary>
        /// <value>true if the previous song in the playlist can be played; otherwise, false.</value>
        public IObservable<bool> CanPlayPreviousSong { get; private set; }

        public IObservable<TimeSpan> CurrentPlaybackTime { get; private set; }

        public Playlist CurrentPlaylist { get; private set; }

        public IObservable<Playlist> CurrentPlaylistChanged
        {
            get { return this.currentPlaylistChanged.AsObservable(); }
        }

        /// <summary>
        /// Gets an observable that reports whether the library is currently looking for new songs
        /// at the song source or removing songs that don't exist anymore.
        /// </summary>
        public IObservable<bool> IsUpdating
        {
            get { return this.isUpdating.DistinctUntilChanged(); }
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
        /// Returns an enumeration of playlists that implements <see cref="INotifyCollectionChanged" />.
        /// </summary>
        public IReadOnlyReactiveList<Playlist> Playlists
        {
            get { return this.playlists; }
        }

        public TimeSpan RemainingPlaylistTimeout
        {
            get
            {
                return this.lastSongAddTime + this.settings.PlaylistTimeout <= DateTime.Now
                           ? TimeSpan.Zero
                           : this.lastSongAddTime - DateTime.Now + this.settings.PlaylistTimeout;
            }
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

        public IObservable<string> SongSourcePath
        {
            get { return this.songSourcePath.AsObservable(); }
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
                Throw.ArgumentNullException(() => name);

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
                Throw.ArgumentNullException(() => songList);

            this.accessControl.VerifyAccess(accessToken);

            this.CurrentPlaylist.AddSongs(songList.ToList()); // Copy the sequence to a list, so that the enumeration doesn't gets modified
        }

        /// <summary>
        /// Adds the song to the end of the playlist. This method throws an exception, if there is
        /// an outstanding timeout.
        /// </summary>
        /// <param name="song">The song to add to the end of the playlist.</param>
        /// <exception cref="InvalidOperationException">There is an outstanding playlist timeout.</exception>
        public void AddSongToPlaylist(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            if (this.settings.EnablePlaylistTimeout && this.RemainingPlaylistTimeout > TimeSpan.Zero)
                throw new InvalidOperationException("Current playlist has a remaining timeout.");

            this.CurrentPlaylist.AddSongs(new[] { song });

            if (this.settings.EnablePlaylistTimeout)
            {
                this.lastSongAddTime = DateTime.Now;
            }
        }

        public void ChangeSongSourcePath(string path, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken);

            if (!this.fileSystem.Directory.Exists(path))
                throw new ArgumentException("Directory does't exist.");

            this.songSourcePath.OnNext(path);
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

            IObservable<Unit> update = this.settings.WhenAnyValue(x => x.SongSourceUpdateInterval)
                .Select(x => Observable.Interval(x, RxApp.TaskpoolScheduler))
                .Switch()
                .Select(_ => Unit.Default)
                .Where(_ => this.settings.EnableAutomaticLibraryUpdates)
                .Merge(this.manualUpdateTrigger)
                .StartWith(Unit.Default);

            update.CombineLatest(this.songSourcePath, (_, path) => path)
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
            this.accessControl.VerifyAccess(accessToken, await this.CurrentPlaylist.CanPlayPreviousSong.FirstAsync());

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
            string pathToSave = this.songSourcePath.Value;

            try
            {
                Utility.Retry(() => this.libraryWriter.Write(songsToSave, playlistsToSave, pathToSave));

                stopWatch.Stop();
                this.Log().Info("Library save took {0}ms", stopWatch.ElapsedMilliseconds);
            }

            catch (LibraryWriteException ex)
            {
                this.Log().FatalException("Couldn't write the library file", ex);
            }

            finally
            {
                stopWatch.Stop();
            }
        }

        public void SetCurrentTime(TimeSpan currentTime, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken);

            this.audioPlayer.CurrentTime = currentTime;
        }

        public void SetVolume(float volume, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken);

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
            this.currentPlaylistChanged.OnNext(playlist);
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
            if (!await this.CurrentPlaylist.CanPlayNextSong.FirstAsync())
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

        private async Task InternPlayNextSongAsync()
        {
            if (!await this.CurrentPlaylist.CanPlayNextSong.FirstAsync() || !this.CurrentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            int nextIndex = this.CurrentPlaylist.CurrentSongIndex.Value + 1;

            await this.InternPlaySongAsync(nextIndex);
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
            }
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

            this.songSourcePath.OnNext(songsPath);

            stopWatch.Stop();
            this.Log().Info("Library load took {0}ms", stopWatch.ElapsedMilliseconds);
        }

        private void RemoveFromLibrary(IEnumerable<LocalSong> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            List<LocalSong> enumerable = songList.ToList();

            // Check if the number of occurences of the artwork key match the number of songs with
            // the same artwork key so we don't delete artwork keys that still have a corresponding
            // song in the library
            Dictionary<string, int> artworkKeys = this.Songs
                .Select(x => x.ArtworkKey.FirstAsync().Wait())
                .Where(x => x != null)
                .GroupBy(x => x)
                .ToDictionary(x => x.Key, x => x.Count());

            var artworkKeysToDelete = enumerable
                .GroupBy(x => x.ArtworkKey.FirstAsync().Wait())
                .Where(x => x != null && x.Key != null && artworkKeys[x.Key] == x.Count())
                .Select(x => x.Key);

            BlobCache.LocalMachine.Invalidate(artworkKeysToDelete).Subscribe();

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

            this.RemoveFromLibrary(removable);

            if (removable.Any())
            {
                this.songsUpdated.OnNext(Unit.Default);
            }
        }

        private async Task StartOnlineArtworkLookup()
        {
            this.Log().Info("Starting online artwork lookup");

            List<LocalSong> songsWithoutArtwork = this.Songs.Where(x => x.ArtworkKey.FirstAsync().Wait() == null).ToList();

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
                    this.Log().ErrorException(string.Format("Error while fetching artwork for {0} - {1}", song.Artist, song.Album), ex);
                }

                if (key != null)
                {
                    song.NotifyArtworkStored(key);
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

            this.isUpdating.OnNext(true);

            await this.RemoveMissingSongsAsync(path);

            ILocalSongFinder songFinder = this.localSongFinderFunc(path);

            this.currentSongFinderSubscription = songFinder.GetSongsAsync()
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(t =>
                {
                    LocalSong song = t.Item1;

                    this.songLock.EnterWriteLock();

                    bool added = this.songs.Add(song);

                    // Inverse the condition, as this case happens way more often and we want to
                    // release the lock as soon as possible We also keep the write lock open so we
                    // can be sure we find the song and it isn't removed in the meanwhile
                    if (!added)
                    {
                        Song existing = this.songs.First(x => x.OriginalPath == song.OriginalPath);

                        bool changed = existing.UpdateMetadataFrom(song);

                        this.songLock.ExitWriteLock();

                        if (changed)
                        {
                            this.songsUpdated.OnNext(Unit.Default);
                        }
                    }

                    else
                    {
                        this.songLock.ExitWriteLock();

                        byte[] artworkData = t.Item2;

                        if (artworkData != null)
                        {
                            ArtworkCache.Instance.Store(artworkData).ToObservable()
                                .Subscribe(song.NotifyArtworkStored);
                        }

                        this.songsUpdated.OnNext(Unit.Default);
                    }
                }, () =>
                {
                    this.StartOnlineArtworkLookup();

                    this.songLock.EnterReadLock();
                    int songCount = this.songs.Count;
                    this.songLock.ExitReadLock();

                    AnalyticsClient.Instance.RecordLibrarySizeAsync(songCount);

                    this.isUpdating.OnNext(false);
                });
        }
    }
}