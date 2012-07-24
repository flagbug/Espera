using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Espera.Core.Audio;
using Rareform.Extensions;
using Rareform.IO;
using Rareform.Validation;

namespace Espera.Core.Management
{
    public class Library : IDisposable
    {
        private readonly AutoResetEvent cacheResetHandle;

        // We need a lock when disposing songs to prevent a modification of the enumeration
        private readonly object disposeLock;

        private readonly RemovableDriveWatcher driveWatcher;
        private readonly List<Playlist> playlists;
        private readonly object songLock;
        private readonly HashSet<Song> songs;
        private bool abortSongAdding;
        private AccessMode accessMode;
        private AudioPlayer currentPlayer;
        private Playlist currentPlayingPlaylist;
        private Playlist currentPlaylist;
        private bool isWaitingOnCache;
        private DateTime lastSongAddTime;
        private bool overrideCurrentCaching;
        private string password;
        private float volume;

        public Library()
        {
            this.songLock = new object();
            this.songs = new HashSet<Song>();
            this.playlists = new List<Playlist>();
            this.volume = 1.0f;
            this.AccessMode = AccessMode.Administrator; // We want implicit to be the administrator, till we change to user mode manually
            this.driveWatcher = RemovableDriveWatcher.Create();
            this.driveWatcher.DriveRemoved += (sender, args) => Task.Factory.StartNew(this.Update);
            this.cacheResetHandle = new AutoResetEvent(false);
            this.disposeLock = new object();
        }

        /// <summary>
        /// Occurs when <see cref="AccessMode"/> property has changed.
        /// </summary>
        public event EventHandler AccessModeChanged;

        /// <summary>
        /// Occurs when the playlist has changed.
        /// </summary>
        public event EventHandler PlaylistChanged;

        /// <summary>
        /// Occurs when a song has been added to the library.
        /// </summary>
        public event EventHandler<LibraryFillEventArgs> SongAdded;

        /// <summary>
        /// Occurs when a song has finished the playback.
        /// </summary>
        public event EventHandler SongFinished;

        /// <summary>
        /// Occurs when a song has started the playback.
        /// </summary>
        public event EventHandler SongStarted;

        /// <summary>
        /// Occurs when the library is finished with updating.
        /// </summary>
        public event EventHandler Updated;

        /// <summary>
        /// Occurs when the library is updating.
        /// </summary>
        public event EventHandler Updating;

        /// <summary>
        /// Gets the access mode that is currently enabled.
        /// </summary>
        public AccessMode AccessMode
        {
            get { return this.accessMode; }
            private set
            {
                if (this.AccessMode != value)
                {
                    this.accessMode = value;
                    this.AccessModeChanged.RaiseSafe(this, EventArgs.Empty);
                }
            }
        }

        public bool CanAddSongToPlaylist
        {
            get { return this.AccessMode == AccessMode.Administrator || this.RemainingPlaylistTimeout <= TimeSpan.Zero; }
        }

        public bool CanChangePlaylist
        {
            get { return CoreSettings.Default.LockPlaylistChanging && this.AccessMode == AccessMode.Administrator; }
        }

        public bool CanChangeTime
        {
            get { return CoreSettings.Default.LockTime && this.AccessMode == AccessMode.Administrator; }
        }

        public bool CanChangeVolume
        {
            get { return CoreSettings.Default.LockVolume && this.AccessMode == AccessMode.Administrator; }
        }

        /// <summary>
        /// Gets a value indicating whether the next song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the next song in the playlist can be played; otherwise, false.
        /// </value>
        public bool CanPlayNextSong
        {
            get { return this.currentPlaylist.CanPlayNextSong; }
        }

        /// <summary>
        /// Gets a value indicating whether the previous song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the previous song in the playlist can be played; otherwise, false.
        /// </value>
        public bool CanPlayPreviousSong
        {
            get { return this.currentPlaylist.CanPlayPreviousSong; }
        }

        public PlaylistInfo CurrentPlaylist
        {
            get { return new PlaylistInfo(this.currentPlaylist); }
        }

        /// <summary>
        /// Gets the index of the currently played song in the playlist.
        /// </summary>
        /// <value>
        /// The index of the currently played song in the playlist.
        /// </value>
        public int? CurrentSongIndex
        {
            get { return this.currentPlaylist.CurrentSongIndex; }
        }

        /// <summary>
        /// Gets or sets the current song's elapsed time.
        /// </summary>
        public TimeSpan CurrentTime
        {
            get { return this.currentPlayer == null ? TimeSpan.Zero : this.currentPlayer.CurrentTime; }
            set
            {
                this.ThrowIfNotAdmin();

                this.currentPlayer.CurrentTime = value;
            }
        }

        public bool EnablePlaylistTimeout
        {
            get { return CoreSettings.Default.EnablePlaylistTimeout; }
            set
            {
                this.ThrowIfNotAdmin();

                CoreSettings.Default.EnablePlaylistTimeout = value;
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
        /// Gets a value indicating whether the playback is paused.
        /// </summary>
        /// <value>
        /// true if the playback is paused; otherwise, false.
        /// </value>
        public bool IsPaused
        {
            get { return this.currentPlayer != null && this.currentPlayer.PlaybackState == AudioPlayerState.Paused; }
        }

        /// <summary>
        /// Gets a value indicating whether the playback is started.
        /// </summary>
        /// <value>
        /// true if the playback is started; otherwise, false.
        /// </value>
        public bool IsPlaying
        {
            get { return this.currentPlayer != null && this.currentPlayer.PlaybackState == AudioPlayerState.Playing; }
        }

        /// <summary>
        /// Gets the song that is currently loaded.
        /// </summary>
        public Song LoadedSong
        {
            get { return this.currentPlayer == null ? null : this.currentPlayer.LoadedSong; }
        }

        public bool LockLibraryRemoval
        {
            get { return CoreSettings.Default.LockLibraryRemoval; }
            set
            {
                this.ThrowIfNotAdmin();

                CoreSettings.Default.LockLibraryRemoval = value;
            }
        }

        public bool LockPlaylistRemoval
        {
            get { return CoreSettings.Default.LockPlaylistRemoval; }
            set
            {
                this.ThrowIfNotAdmin();

                CoreSettings.Default.LockPlaylistRemoval = value;
            }
        }

        public bool LockPlayPause
        {
            get { return CoreSettings.Default.LockPlayPause; }
            set
            {
                this.ThrowIfNotAdmin();

                CoreSettings.Default.LockPlayPause = value;
            }
        }

        public bool LockTime
        {
            get { return CoreSettings.Default.LockTime; }
            set
            {
                this.ThrowIfNotAdmin();

                CoreSettings.Default.LockTime = value;
            }
        }

        public bool LockVolume
        {
            get { return CoreSettings.Default.LockVolume; }
            set
            {
                this.ThrowIfNotAdmin();

                CoreSettings.Default.LockVolume = value;
            }
        }

        public IEnumerable<PlaylistInfo> Playlists
        {
            get { return this.playlists.Select(playlist => new PlaylistInfo(playlist)); }
        }

        public TimeSpan PlaylistTimeout
        {
            get { return CoreSettings.Default.PlaylistTimeout; }
            set
            {
                this.ThrowIfNotAdmin();

                CoreSettings.Default.PlaylistTimeout = value;
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
                lock (songLock)
                {
                    return this.songs;
                }
            }
        }

        public bool StreamYoutube
        {
            get { return CoreSettings.Default.StreamYoutube; }
            set
            {
                this.ThrowIfNotAdmin();

                CoreSettings.Default.StreamYoutube = value;
            }
        }

        /// <summary>
        /// Gets the duration of the current song.
        /// </summary>
        public TimeSpan TotalTime
        {
            get { return this.currentPlayer == null ? TimeSpan.Zero : this.currentPlayer.TotalTime; }
        }

        /// <summary>
        /// Gets or sets the current volume.
        /// </summary>
        /// <value>
        /// The current volume.
        /// </value>
        public float Volume
        {
            get { return this.currentPlayer == null ? this.volume : this.currentPlayer.Volume; }
            set
            {
                this.ThrowIfNotAdmin();

                this.volume = value;

                if (this.currentPlayer != null)
                {
                    this.currentPlayer.Volume = value;
                }
            }
        }

        /// <summary>
        /// Adds a new playlist to the library and immediately sets it as the current playlist.
        /// </summary>
        /// <param name="name">The name of the playlist, It is required that no other playlist has this name.</param>
        /// <exception cref="InvalidOperationException">A playlist with the specified name already exists.</exception>
        public void AddAndChangeToPlaylist(string name)
        {
            this.AddPlaylist(name);
            this.ChangeToPlaylist(name);
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
            if (this.playlists.Any(playlist => playlist.Name == name))
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

            this.currentPlaylist.AddSongs(songList.ToList()); // Copy the sequence to a list, so that the enumeration doesn't gets modified

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

            this.currentPlaylist.AddSongs(new[] { song });

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
                throw new InvalidPasswordException("The password is incorrect.");

            this.AccessMode = AccessMode.Administrator;
        }

        /// <summary>
        /// Changes the access mode to party mode.
        /// </summary>
        public void ChangeToParty()
        {
            this.AccessMode = AccessMode.Party;
        }

        public void ChangeToPlaylist(string name)
        {
            if (CoreSettings.Default.LockPlaylistChanging && this.AccessMode == AccessMode.Party)
                throw new InvalidOperationException("Not allowed to change playlist when in party mode.");

            this.currentPlaylist = this.playlists.Single(playlist => playlist.Name == name);
        }

        /// <summary>
        /// Continues the currently loaded song.
        /// </summary>
        public void ContinueSong()
        {
            this.ThrowIfNotAdmin();

            this.currentPlayer.Play();
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.currentPlayer != null)
            {
                this.currentPlayer.Dispose();
            }

            this.driveWatcher.Dispose();

            this.abortSongAdding = true;

            this.cacheResetHandle.Dispose();

            lock (this.disposeLock)
            {
                DisposeSongs(this.songs);
            }

            CoreSettings.Default.Save();
        }

        /// <summary>
        /// Pauses the currently loaded song.
        /// </summary>
        public void PauseSong()
        {
            if (this.LockPlayPause && this.AccessMode == AccessMode.Party)
                throw new InvalidOperationException("Not allowed to play when in party mode.");

            this.currentPlayer.Pause();
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

            if (!this.currentPlaylist.CanPlayPreviousSong || !this.currentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The previous song couldn't be played.");

            this.PlaySong(this.currentPlaylist.CurrentSongIndex.Value - 1);
        }

        /// <summary>
        /// Plays the song with the specified index in the playlist.
        /// </summary>
        /// <param name="playlistIndex">The index of the song in the playlist.</param>
        public void PlaySong(int playlistIndex)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            if (this.LockPlayPause && this.AccessMode == AccessMode.Party)
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

            if (this.LockLibraryRemoval && this.AccessMode == AccessMode.Party)
                throw new InvalidOperationException("Not allowed to remove songs when in party mode.");

            DisposeSongs(songList);

            lock (this.songLock)
            {
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

            if (this.LockPlaylistRemoval && this.AccessMode == AccessMode.Party)
                throw new InvalidOperationException("Not allowed to remove songs when in party mode.");

            bool stopCurrentSong = indexes.Any(index => index == this.currentPlaylist.CurrentSongIndex);

            this.currentPlaylist.RemoveSongs(indexes);

            this.PlaylistChanged.RaiseSafe(this, EventArgs.Empty);

            if (stopCurrentSong)
            {
                this.currentPlayer.Stop();
                this.SongFinished.RaiseSafe(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Removes the specified songs from the playlist.
        /// </summary>
        /// <param name="songList">The songs to remove.</param>
        public void RemoveFromPlaylist(IEnumerable<Song> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            this.RemoveFromPlaylist(this.currentPlaylist.GetIndexes(songList));
        }

        /// <summary>
        /// Removed the playlist with the specified name.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to remove.</param>
        /// <exception cref="InvalidOperationException">No playlist exists, or np playlist with the specified name exists.</exception>
        public void RemovePlaylist(string playlistName)
        {
            if (playlistName == null)
                Throw.ArgumentNullException(() => playlistName);

            if (!this.Playlists.Any())
                throw new InvalidOperationException("There are no playlists.");

            int removed = this.playlists.RemoveAll(playlist => playlist.Name == playlistName);

            if (removed == 0)
                throw new InvalidOperationException("No playlist with the specified name exists.");
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

            finder.SongFound += (sender, e) =>
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
                        added = this.songs.Add(e.Song);
                    }
                }

                if (added)
                {
                    this.SongAdded.RaiseSafe(this, new LibraryFillEventArgs(e.Song, finder.TagsProcessed, finder.CurrentTotalSongs));
                }
            };

            finder.Start();
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

        private void HandleSongFinish()
        {
            if (!this.currentPlaylist.CanPlayNextSong)
            {
                this.currentPlaylist.CurrentSongIndex = null;
            }

            this.SongFinished.RaiseSafe(this, EventArgs.Empty);

            if (this.currentPlaylist.CanPlayNextSong)
            {
                this.InternPlayNextSong();
            }
        }

        private void InternPlayNextSong()
        {
            if (!this.currentPlaylist.CanPlayNextSong || !this.currentPlaylist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            int nextIndex = this.currentPlaylist.CurrentSongIndex.Value + 1;
            Song nextSong = this.currentPlaylist[nextIndex];

            // We want the to swap the songs, if the song that should be played next is currently caching
            if (nextSong.HasToCache && !nextSong.IsCached && this.currentPlaylist.ContainsIndex(nextIndex + 1))
            {
                var nextReady = this.currentPlaylist
                    .Select((song, i) => new { Song = song, Index = i })
                    .Skip(nextIndex)
                    .FirstOrDefault(item => !item.Song.HasToCache || item.Song.IsCached);

                if (nextReady != null)
                {
                    this.currentPlaylist.InsertMove(nextReady.Index, nextIndex);
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

            if (this.currentPlayingPlaylist != null)
            {
                this.currentPlayingPlaylist.CurrentSongIndex = null;
            }

            this.currentPlayingPlaylist = this.currentPlaylist;

            this.currentPlaylist.CurrentSongIndex = playlistIndex;

            Song song = this.currentPlaylist[playlistIndex];

            if (this.currentPlayer != null)
            {
                this.currentPlayer.Dispose();
            }

            this.currentPlayer = song.CreateAudioPlayer();

            this.currentPlayer.SongFinished += (sender, e) => this.HandleSongFinish();
            this.currentPlayer.Volume = this.volume;

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

                this.currentPlayer.Load(song);
                this.currentPlayer.Play();

                this.SongStarted.RaiseSafe(this, EventArgs.Empty);
            });
        }

        private void ThrowIfNotAdmin()
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("Not in administrator mode.");
        }

        private void Update()
        {
            this.Updating.RaiseSafe(this, EventArgs.Empty);

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

            this.Updated.RaiseSafe(this, EventArgs.Empty);
        }
    }
}