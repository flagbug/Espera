using Espera.Core.Audio;
using Espera.Core.Settings;
using Rareform.Extensions;
using Rareform.Validation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Core.Management
{
    public sealed class Library : IDisposable
    {
        private readonly BehaviorSubject<AccessMode> accessModeSubject;
        private readonly AutoResetEvent cacheResetHandle;

        private readonly BehaviorSubject<AudioPlayer> currentPlayer;
        private readonly Subject<Playlist> currentPlaylistChanged;

        // We need a lock when disposing songs to prevent a modification of the enumeration
        private readonly object disposeLock;

        private readonly IRemovableDriveWatcher driveWatcher;
        private readonly Subject<bool> isUpdating;
        private readonly ILibraryReader libraryReader;
        private readonly ILibraryWriter libraryWriter;
        private readonly ObservableCollection<Playlist> playlists;
        private readonly ReadOnlyObservableCollection<Playlist> publicPlaylistWrapper;
        private readonly ILibrarySettings settings;
        private readonly object songLock;
        private readonly HashSet<Song> songs;
        private bool abortSongAdding;
        private AccessMode accessMode;
        private Playlist currentPlayingPlaylist;
        private Playlist instantPlaylist;
        private bool isWaitingOnCache;
        private DateTime lastSongAddTime;
        private bool overrideCurrentCaching;
        private string password;

        public Library(IRemovableDriveWatcher driveWatcher, ILibraryReader libraryReader, ILibraryWriter libraryWriter, ILibrarySettings settings)
        {
            this.songLock = new object();
            this.songs = new HashSet<Song>();
            this.playlists = new ObservableCollection<Playlist>();
            this.publicPlaylistWrapper = new ReadOnlyObservableCollection<Playlist>(this.playlists);
            this.currentPlaylistChanged = new Subject<Playlist>();
            this.accessModeSubject = new BehaviorSubject<AccessMode>(Management.AccessMode.Administrator); // We want implicit to be the administrator, till we change to user mode manually
            this.accessMode = Management.AccessMode.Administrator;
            this.cacheResetHandle = new AutoResetEvent(false);
            this.driveWatcher = driveWatcher;
            this.libraryReader = libraryReader;
            this.libraryWriter = libraryWriter;
            this.disposeLock = new object();
            this.settings = settings;
            this.isUpdating = new Subject<bool>();
            this.CanPlayNextSong = this.CurrentPlaylistChanged.Select(x => x.CanPlayNextSong).Switch();
            this.CanPlayPreviousSong = this.CurrentPlaylistChanged.Select(x => x.CanPlayPreviousSong).Switch();
            this.currentPlayer = new BehaviorSubject<AudioPlayer>(null);
            this.LoadedSong = this.currentPlayer
                .Select(x => x == null ? null : x.Song);
            this.TotalTime = this.currentPlayer
                .Select(x => x == null ? Observable.Never(TimeSpan.Zero) : x.TotalTime)
                .Switch()
                .StartWith(TimeSpan.Zero);
            this.PlaybackState = this.currentPlayer
                .Select(x => x == null ? Observable.Never(AudioPlayerState.None) : x.PlaybackState)
                .Switch()
                .StartWith(AudioPlayerState.None);
        }

        /// <summary>
        /// Occurs when the playlist has changed.
        /// </summary>
        public event EventHandler PlaylistChanged;

        /// <summary>
        /// Occurs when a song has been added to the library.
        /// </summary>
        public event EventHandler<LibraryFillEventArgs> SongAdded;

        /// <summary>
        /// Occurs when a corrupted song has been attempted to be played.
        /// </summary>
        public event EventHandler SongCorrupted;

        /// <summary>
        /// Occurs when a song has finished the playback.
        /// </summary>
        public event EventHandler SongFinished;

        /// <summary>
        /// Occurs when a song has started the playback.
        /// </summary>
        public event EventHandler SongStarted;

        public event EventHandler VideoPlayerCallbackChanged;

        /// <summary>
        /// Gets the access mode that is currently enabled.
        /// </summary>
        public IObservable<AccessMode> AccessMode
        {
            get { return this.accessModeSubject.AsObservable(); }
        }

        public bool CanAddSongToPlaylist
        {
            get { return this.accessMode == Management.AccessMode.Administrator || this.RemainingPlaylistTimeout <= TimeSpan.Zero; }
        }

        public bool CanChangeTime
        {
            get { return this.accessMode == Management.AccessMode.Administrator || !this.LockTime; }
        }

        public bool CanChangeVolume
        {
            get { return this.accessMode == Management.AccessMode.Administrator || !this.LockVolume; }
        }

        /// <summary>
        /// Gets a value indicating whether the next song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the next song in the playlist can be played; otherwise, false.
        /// </value>
        public IObservable<bool> CanPlayNextSong { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the previous song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the previous song in the playlist can be played; otherwise, false.
        /// </value>
        public IObservable<bool> CanPlayPreviousSong { get; private set; }

        public bool CanSwitchPlaylist
        {
            get { return this.accessMode == Management.AccessMode.Administrator || !this.LockPlaylistSwitching; }
        }

        public Playlist CurrentPlaylist { get; private set; }

        public IObservable<Playlist> CurrentPlaylistChanged
        {
            get { return this.currentPlaylistChanged.AsObservable(); }
        }

        /// <summary>
        /// Gets or sets the current song's elapsed time.
        /// </summary>
        public TimeSpan CurrentTime
        {
            get { return this.currentPlayer.First() == null ? TimeSpan.Zero : this.currentPlayer.First().CurrentTime; }
            set
            {
                this.ThrowIfNotAdmin();

                if (this.currentPlayer.First() != null)
                {
                    this.currentPlayer.First().CurrentTime = value;
                }
            }
        }

        public bool EnablePlaylistTimeout
        {
            get { return this.settings.EnablePlaylistTimeout; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.EnablePlaylistTimeout = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the administrator is created.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if the administrator is created; otherwise, <c>false</c>.
        /// </value>
        public bool IsAdministratorCreated { get; private set; }

        /// <summary>
        /// Occurs when the library is updating. <c>True</c>, when the updated starts, <c>false</c> when the update finished.
        /// </summary>
        public IObservable<bool> IsUpdating
        {
            get { return this.isUpdating.AsObservable(); }
        }

        /// <summary>
        /// Gets the song that is currently loaded.
        /// </summary>
        public IObservable<Song> LoadedSong { get; private set; }

        public bool LockLibraryRemoval
        {
            get { return this.settings.LockLibraryRemoval; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.LockLibraryRemoval = value;
            }
        }

        public bool LockPlaylistRemoval
        {
            get { return this.settings.LockPlaylistRemoval; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.LockPlaylistRemoval = value;
            }
        }

        public bool LockPlaylistSwitching
        {
            get { return this.settings.LockPlaylistSwitching; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.LockPlaylistSwitching = value;
            }
        }

        public bool LockPlayPause
        {
            get { return this.settings.LockPlayPause; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.LockPlayPause = value;
            }
        }

        public bool LockTime
        {
            get { return this.settings.LockTime; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.LockTime = value;
            }
        }

        public bool LockVolume
        {
            get { return this.settings.LockVolume; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.LockVolume = value;
            }
        }

        public IObservable<AudioPlayerState> PlaybackState { get; private set; }

        /// <summary>
        /// Returns an enumeration of playlists that implements <see cref="INotifyCollectionChanged"/>.
        /// </summary>
        public ReadOnlyObservableCollection<Playlist> Playlists
        {
            get { return this.publicPlaylistWrapper; }
        }

        public TimeSpan PlaylistTimeout
        {
            get { return this.settings.PlaylistTimeout; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.PlaylistTimeout = value;
            }
        }

        public TimeSpan RemainingPlaylistTimeout
        {
            get
            {
                return this.lastSongAddTime + this.PlaylistTimeout <= DateTime.Now
                           ? TimeSpan.Zero
                           : this.lastSongAddTime - DateTime.Now + this.PlaylistTimeout;
            }
        }

        /// <summary>
        /// Gets all songs that are currently in the library.
        /// </summary>
        public IEnumerable<Song> Songs
        {
            get
            {
                IEnumerable<Song> tempSongs;

                lock (songLock)
                {
                    tempSongs = this.songs.ToList();
                }

                return tempSongs;
            }
        }

        public bool StreamYoutube
        {
            get { return this.settings.StreamYoutube; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.StreamYoutube = value;
            }
        }

        /// <summary>
        /// Gets the duration of the current song.
        /// </summary>
        public IObservable<TimeSpan> TotalTime { get; private set; }

        public IVideoPlayerCallback VideoPlayerCallback
        {
            get { return this.currentPlayer.First() as IVideoPlayerCallback; }
        }

        /// <summary>
        /// Gets or sets the current volume.
        /// </summary>
        /// <value>
        /// The current volume.
        /// </value>
        public float Volume
        {
            get { return this.settings.Volume; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.Volume = value;

                if (this.currentPlayer.First() != null)
                {
                    this.currentPlayer.First().Volume = value;
                }
            }
        }

        /// <summary>
        /// Adds a new playlist to the library and immediately sets it as the current playlist.
        /// </summary>
        /// <param name="name">The name of the playlist, It is required that no other playlist has this name.</param>
        /// <exception cref="InvalidOperationException">A playlist with the specified name already exists.</exception>
        public void AddAndSwitchToPlaylist(string name)
        {
            this.AddPlaylist(name);
            this.SwitchToPlaylist(this.GetPlaylistByName(name));
        }

        /// <summary>
        /// Adds the song that are contained in the specified directory recursively in an asynchronous manner to the library.
        /// </summary>
        /// <param name="path">The path of the directory to search.</param>
        /// <returns>The <see cref="Task"/> that did the work.</returns>
        public Task AddLocalSongsAsync(string path)
        {
            if (path == null)
                Throw.ArgumentNullException(() => path);

            return Task.Factory.StartNew(() => this.AddLocalSongs(path));
        }

        /// <summary>
        /// Adds a new playlist with the specified name to the library.
        /// </summary>
        /// <param name="name">The name of the playlist. It is required that no other playlist has this name.</param>
        /// <exception cref="InvalidOperationException">A playlist with the specified name already exists.</exception>
        public void AddPlaylist(string name)
        {
            if (name == null)
                Throw.ArgumentNullException(() => name);

            if (this.GetPlaylistByName(name) != null)
                throw new InvalidOperationException("A playlist with this name already exists.");

            this.playlists.Add(new Playlist(name));
        }

        /// <summary>
        /// Adds the specified song to the end of the playlist.
        /// This method is only available in administrator mode.
        /// </summary>
        /// <param name="songList">The songs to add to the end of the playlist.</param>
        public void AddSongsToPlaylist(IEnumerable<Song> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            this.ThrowIfNotAdmin();

            this.CurrentPlaylist.AddSongs(songList.ToList()); // Copy the sequence to a list, so that the enumeration doesn't gets modified

            this.PlaylistChanged.RaiseSafe(this, EventArgs.Empty);
        }

        /// <summary>
        /// Adds the song to the end of the playlist.
        /// This method throws an exception, if there is an outstanding timeout.
        /// </summary>
        /// <param name="song">The song to add to the end of the playlist.</param>
        public void AddSongToPlaylist(Song song)
        {
            if (song == null)
                Throw.ArgumentNullException(() => song);

            this.CurrentPlaylist.AddSongs(new[] { song });

            this.lastSongAddTime = DateTime.Now;

            this.PlaylistChanged.RaiseSafe(this, EventArgs.Empty);
        }

        /// <summary>
        /// Logs the administrator with the specified password in.
        /// </summary>
        /// <param name="adminPassword">The administrator password.</param>
        public void ChangeToAdmin(string adminPassword)
        {
            if (adminPassword == null)
                Throw.ArgumentNullException(() => adminPassword);

            if (this.password != adminPassword)
                throw new WrongPasswordException("The password is incorrect.");

            this.accessMode = Management.AccessMode.Administrator;
            this.accessModeSubject.OnNext(Management.AccessMode.Administrator);
        }

        /// <summary>
        /// Changes the access mode to party mode.
        /// </summary>
        public void ChangeToParty()
        {
            if (!this.IsAdministratorCreated)
                throw new InvalidOperationException("Administrator is not created.");

            this.accessMode = Management.AccessMode.Party;
            this.accessModeSubject.OnNext(Management.AccessMode.Party);
        }

        /// <summary>
        /// Continues the currently loaded song.
        /// </summary>
        public void ContinueSong()
        {
            this.ThrowIfNotAdmin();

            this.currentPlayer.First().Play();
        }

        /// <summary>
        /// Creates the administrator with the specified password.
        /// </summary>
        /// <param name="adminPassword">The administrator password.</param>
        public void CreateAdmin(string adminPassword)
        {
            if (adminPassword == null)
                Throw.ArgumentNullException(() => adminPassword);

            if (String.IsNullOrWhiteSpace(adminPassword))
                Throw.ArgumentException("Password cannot consist only of whitespaces.", () => adminPassword);

            if (this.IsAdministratorCreated)
                throw new InvalidOperationException("The administrator is already created.");

            this.password = adminPassword;
            this.IsAdministratorCreated = true;
        }

        public void Dispose()
        {
            if (this.currentPlayer.First() != null)
            {
                this.currentPlayer.First().Dispose();
            }

            this.driveWatcher.Dispose();

            this.abortSongAdding = true;

            this.cacheResetHandle.Dispose();

            lock (this.disposeLock)
            {
                DisposeSongs(this.songs);
            }

            this.settings.Save();
        }

        public Playlist GetPlaylistByName(string playlistName)
        {
            if (playlistName == null)
                Throw.ArgumentNullException(() => playlistName);

            return this.playlists.FirstOrDefault(playlist => playlist.Name == playlistName);
        }

        public void Initialize()
        {
            if (this.settings.UpgradeRequired)
            {
                this.settings.Upgrade();
                this.settings.UpgradeRequired = false;
                this.settings.Save();
            }

            this.driveWatcher.Initialize();
            this.driveWatcher.DriveRemoved += (sender, args) => Task.Factory.StartNew(this.Update);

            this.Load();
        }

        /// <summary>
        /// Pauses the currently loaded song.
        /// </summary>
        public void PauseSong()
        {
            if (this.LockPlayPause && this.accessMode == Management.AccessMode.Party)
                throw new InvalidOperationException("Not allowed to play when in party mode.");

            this.currentPlayer.First().Pause();
        }

        public void PlayInstantly(IEnumerable<Song> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            if (this.instantPlaylist != null)
            {
                this.playlists.Remove(instantPlaylist);
            }

            string instantPlaylistName = Guid.NewGuid().ToString();
            this.instantPlaylist = new Playlist(instantPlaylistName, true);

            this.playlists.Add(this.instantPlaylist);
            this.SwitchToPlaylist(this.instantPlaylist);

            this.AddSongsToPlaylist(songList);

            this.PlaySong(0);
        }

        /// <summary>
        /// Plays the next song in the playlist.
        /// </summary>
        public void PlayNextSong()
        {
            this.ThrowIfNotAdmin();

            this.InternPlayNextSong();
        }

        /// <summary>
        /// Plays the previous song in the playlist.
        /// </summary>
        public void PlayPreviousSong()
        {
            this.ThrowIfNotAdmin();

            if (!this.CurrentPlaylist.CanPlayPreviousSong.First() || !this.CurrentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The previous song couldn't be played.");

            this.PlaySong(this.CurrentPlaylist.CurrentSongIndex.Value - 1);
        }

        /// <summary>
        /// Plays the song with the specified index in the playlist.
        /// </summary>
        /// <param name="playlistIndex">The index of the song in the playlist.</param>
        public void PlaySong(int playlistIndex)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            if (this.LockPlayPause && this.accessMode == Management.AccessMode.Party)
                throw new InvalidOperationException("Not allowed to play when in party mode.");

            this.InternPlaySong(playlistIndex);
        }

        /// <summary>
        /// Removes the specified songs from the library.
        /// </summary>
        /// <param name="songList">The list of the songs to remove from the library.</param>
        public void RemoveFromLibrary(IEnumerable<Song> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            if (this.LockLibraryRemoval && this.accessMode == Management.AccessMode.Party)
                throw new InvalidOperationException("Not allowed to remove songs when in party mode.");

            DisposeSongs(songList);

            lock (this.songLock)
            {
                this.playlists.ForEach(playlist => this.RemoveFromPlaylist(playlist, songList));

                foreach (Song song in songList)
                {
                    this.songs.Remove(song);
                }
            }
        }

        /// <summary>
        /// Removes the songs with the specified indexes from the playlist.
        /// </summary>
        /// <param name="indexes">The indexes of the songs to remove from the playlist.</param>
        public void RemoveFromPlaylist(IEnumerable<int> indexes)
        {
            if (indexes == null)
                Throw.ArgumentNullException(() => indexes);

            if (this.LockPlaylistRemoval && this.accessMode == Management.AccessMode.Party)
                throw new InvalidOperationException("Not allowed to remove songs when in party mode.");

            this.RemoveFromPlaylist(this.CurrentPlaylist, indexes);
        }

        /// <summary>
        /// Removes the specified songs from the playlist.
        /// </summary>
        /// <param name="songList">The songs to remove.</param>
        public void RemoveFromPlaylist(IEnumerable<Song> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            this.RemoveFromPlaylist(this.CurrentPlaylist, songList);
        }

        /// <summary>
        /// Removes the playlist with the specified name from the library.
        /// </summary>
        /// <param name="playlist">The playlist to remove.</param>
        public void RemovePlaylist(Playlist playlist)
        {
            if (playlist == null)
                Throw.ArgumentNullException(() => playlist);

            this.playlists.Remove(playlist);
        }

        public void Save()
        {
            HashSet<LocalSong> casted;

            lock (this.songLock)
            {
                casted = new HashSet<LocalSong>(this.songs.Cast<LocalSong>());
            }

            // Clean up the songs that aren't in the library anymore
            // This happens when the user adds a song from a removable device to the playlistx
            // then removes the device, so the song is removed from the library, but not from the playlist
            foreach (Playlist playlist in this.playlists)
            {
                List<Song> removable = playlist.OfType<LocalSong>().Where(song => !casted.Contains(song)).Cast<Song>().ToList();

                IEnumerable<int> indexes = playlist.GetIndexes(removable);

                playlist.RemoveSongs(indexes);
            }

            this.libraryWriter.Write(casted, this.playlists.Where(playlist => !playlist.IsTemporary));
        }

        public void ShufflePlaylist()
        {
            this.CurrentPlaylist.Shuffle();
        }

        public void SwitchToPlaylist(Playlist playlist)
        {
            if (playlist == null)
                Throw.ArgumentNullException(() => playlist);

            if (!this.CanSwitchPlaylist)
                throw new InvalidOperationException("Not allowed to switch playlist.");

            this.CurrentPlaylist = playlist;
            this.currentPlaylistChanged.OnNext(playlist);
        }

        /// <summary>
        /// Disposes the all songs and clear their cache.
        /// </summary>
        /// <param name="songList">The songs to dispose.</param>
        private static void DisposeSongs(IEnumerable<Song> songList)
        {
            // If the condition is removed, every file that has been added to the library will be deleted...
            foreach (Song song in songList.Where(song => song.HasToCache && song.IsCached))
            {
                try
                {
                    song.ClearCache();
                }

                catch (IOException)
                {
                    // Swallow the exception, we don't care about temporary files that could not be deleted
                }
            }
        }

        /// <summary>
        /// Adds the song that are contained in the specified directory recursively to the library.
        /// </summary>
        /// <param name="path">The path of the directory to search.</param>
        private void AddLocalSongs(string path)
        {
            if (path == null)
                Throw.ArgumentNullException(() => path);

            if (!Directory.Exists(path))
                Throw.ArgumentException("The directory doesn't exist.", () => path);

            var finder = new LocalSongFinder(path);

            int totalSongs = 0;
            int songsProcessed = 0;

            finder.SongsFound.Subscribe(i => totalSongs = i);

            finder.SongFound
                .Subscribe(song =>
                {
                    if (this.abortSongAdding)
                    {
                        finder.Abort();
                        return;
                    }

                    bool added;

                    lock (this.songLock)
                    {
                        lock (this.disposeLock)
                        {
                            added = this.songs.Add(song);
                        }
                    }

                    if (added)
                    {
                        songsProcessed++;
                        this.SongAdded.RaiseSafe(this, new LibraryFillEventArgs(song, songsProcessed, totalSongs));
                    }
                });

            finder.Execute();
        }

        private bool AwaitCaching(Song song)
        {
            this.isWaitingOnCache = true;

            while (!song.IsCached)
            {
                if (this.overrideCurrentCaching)
                {
                    // If we wait on a song that is currently caching, but the user wants to play an other song,
                    // let the other song pass and discard the waiting of the current song
                    this.isWaitingOnCache = false;
                    this.overrideCurrentCaching = false;
                    this.cacheResetHandle.Set();
                    return false;
                }

                Thread.Sleep(250);
            }

            this.isWaitingOnCache = false;

            return true;
        }

        private void HandleSongCorruption()
        {
            if (!this.CurrentPlaylist.CanPlayNextSong.First())
            {
                this.CurrentPlaylist.CurrentSongIndex = null;
            }

            else
            {
                this.InternPlayNextSong();
            }
        }

        private void HandleSongFinish()
        {
            if (!this.CurrentPlaylist.CanPlayNextSong.First())
            {
                this.CurrentPlaylist.CurrentSongIndex = null;
            }

            this.currentPlayer.First().Dispose();
            this.currentPlayer.OnNext(null);

            this.SongFinished.RaiseSafe(this, EventArgs.Empty);

            if (this.CurrentPlaylist.CanPlayNextSong.First())
            {
                this.InternPlayNextSong();
            }
        }

        private void InternPlayNextSong()
        {
            if (!this.CurrentPlaylist.CanPlayNextSong.First() || !this.CurrentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            int nextIndex = this.CurrentPlaylist.CurrentSongIndex.Value + 1;
            Song nextSong = this.CurrentPlaylist[nextIndex].Song;

            // We want the to swap the songs, if the song that should be played next is currently caching
            if (nextSong.HasToCache && !nextSong.IsCached && this.CurrentPlaylist.ContainsIndex(nextIndex + 1))
            {
                var nextReady = this.CurrentPlaylist
                    .Skip(nextIndex)
                    .FirstOrDefault(entry => !entry.Song.HasToCache || entry.Song.IsCached);

                if (nextReady != null)
                {
                    this.CurrentPlaylist.InsertMove(nextReady.Index, nextIndex);
                }
            }

            this.InternPlaySong(nextIndex);
        }

        private void InternPlaySong(int playlistIndex)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            if (this.isWaitingOnCache)
            {
                this.overrideCurrentCaching = true;

                // Let the song that is selected to be played wait here, if there is currently another song caching
                cacheResetHandle.WaitOne();
            }

            if (this.currentPlayingPlaylist != null && this.currentPlayingPlaylist != this.CurrentPlaylist)
            {
                this.currentPlayingPlaylist.CurrentSongIndex = null;
            }

            this.currentPlayingPlaylist = this.CurrentPlaylist;

            this.CurrentPlaylist.CurrentSongIndex = playlistIndex;

            Song song = this.CurrentPlaylist[playlistIndex].Song;

            this.RenewCurrentPlayer(song);

            Task.Factory.StartNew(() =>
            {
                if (song.HasToCache && !song.IsCached)
                {
                    bool cached = this.AwaitCaching(song);

                    if (!cached)
                    {
                        return;
                    }
                }

                this.overrideCurrentCaching = false;

                try
                {
                    this.currentPlayer.First().Load();
                }

                catch (SongLoadException)
                {
                    song.IsCorrupted = true;
                    this.SongCorrupted.RaiseSafe(this, EventArgs.Empty);

                    this.HandleSongCorruption();

                    return;
                }

                try
                {
                    this.currentPlayer.First().Play();
                }

                catch (PlaybackException)
                {
                    song.IsCorrupted = true;
                    this.SongCorrupted.RaiseSafe(this, EventArgs.Empty);

                    this.HandleSongCorruption();

                    return;
                }

                this.SongStarted.RaiseSafe(this, EventArgs.Empty);
            });
        }

        private void Load()
        {
            IEnumerable<Song> savedSongs = this.libraryReader.ReadSongs();

            foreach (Song song in savedSongs)
            {
                this.songs.Add(song);
            }

            IEnumerable<Playlist> savedPlaylists = this.libraryReader.ReadPlaylists();

            foreach (Playlist playlist in savedPlaylists)
            {
                this.playlists.Add(playlist);
            }
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<int> indexes)
        {
            bool stopCurrentSong = playlist == this.CurrentPlaylist && indexes.Any(index => index == this.CurrentPlaylist.CurrentSongIndex);

            playlist.RemoveSongs(indexes);

            this.PlaylistChanged.RaiseSafe(this, EventArgs.Empty);

            if (stopCurrentSong)
            {
                this.currentPlayer.First().Stop();
                this.SongFinished.RaiseSafe(this, EventArgs.Empty);
            }
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<Song> songList)
        {
            this.RemoveFromPlaylist(playlist, playlist.GetIndexes(songList));
        }

        private void RenewCurrentPlayer(Song song)
        {
            if (this.currentPlayer.First() != null)
            {
                this.currentPlayer.First().Dispose();
            }

            this.currentPlayer.OnNext(song.CreateAudioPlayer());

            if (this.currentPlayer.First() is IVideoPlayerCallback)
            {
                this.VideoPlayerCallbackChanged.RaiseSafe(this, EventArgs.Empty);
            }

            this.currentPlayer.First().SongFinished.Subscribe(x => this.HandleSongFinish());
            this.currentPlayer.First().Volume = this.Volume;
        }

        private void ThrowIfNotAdmin()
        {
            if (this.accessMode != Management.AccessMode.Administrator)
                throw new InvalidOperationException("Not in administrator mode.");
        }

        private void Update()
        {
            this.isUpdating.OnNext(true);

            IEnumerable<Song> removable;

            lock (this.songLock)
            {
                removable = this.songs
                    .Where(song => !File.Exists(song.OriginalPath))
                    .ToList();
            }

            DisposeSongs(removable);

            foreach (Song song in removable)
            {
                lock (this.songLock)
                {
                    this.songs.Remove(song);
                }
            }

            this.isUpdating.OnNext(false);
        }
    }
}