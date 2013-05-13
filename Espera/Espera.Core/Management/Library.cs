using Espera.Core.Audio;
using Espera.Core.Settings;
using Rareform.Extensions;
using Rareform.Validation;
using ReactiveMarrow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive;
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
        private readonly object disposeLock; // We need a lock when disposing songs to prevent a modification of the enumeration
        private readonly IRemovableDriveWatcher driveWatcher;
        private readonly ILibraryReader libraryReader;
        private readonly ILibraryWriter libraryWriter;
        private readonly ObservableCollection<Playlist> playlists;
        private readonly ReadOnlyObservableCollection<Playlist> publicPlaylistWrapper;
        private readonly ILibrarySettings settings;
        private readonly object songLock;
        private readonly HashSet<Song> songs;
        private readonly ObservableCollection<string> songSourcePaths;
        private readonly Subject<Unit> songStarted;
        private readonly Subject<Unit> songsUpdated;
        private bool abortUpdate;
        private AccessMode accessMode;
        private Playlist currentPlayingPlaylist;
        private Playlist instantPlaylist;
        private bool isUpdatingSources;
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
            this.CanPlayNextSong = this.currentPlaylistChanged.Select(x => x.CanPlayNextSong).Switch();
            this.CanPlayPreviousSong = this.currentPlaylistChanged.Select(x => x.CanPlayPreviousSong).Switch();
            this.currentPlayer = new BehaviorSubject<AudioPlayer>(null);
            this.songStarted = new Subject<Unit>();
            this.songSourcePaths = new ObservableCollection<string>();
            this.SongSourcePaths = new ReadOnlyObservableCollection<string>(this.songSourcePaths);
            this.songsUpdated = new Subject<Unit>();

            this.LoadedSong = this.currentPlayer
                .Select(x => x == null ? null : x.Song);

            this.TotalTime = this.currentPlayer
                .Select(x => x == null ? Observable.Return(TimeSpan.Zero) : x.TotalTime)
                .Switch()
                .StartWith(TimeSpan.Zero);

            this.PlaybackState = this.currentPlayer
                .Select(x => x == null ? Observable.Return(AudioPlayerState.None) : x.PlaybackState)
                .Switch()
                .StartWith(AudioPlayerState.None);

            this.VideoPlayerCallback = this.currentPlayer
                .OfType<IVideoPlayerCallback>();

            this.currentPlayer
                .Where(x => x != null)
                .Select(x => x.PlaybackState.Where(p => p == AudioPlayerState.Finished).Select(q => x))
                .Switch()
                .Subscribe(this.HandleSongFinish);

            /*
             * Start boring, repeating glue code
             */
            this.EnablePlaylistTimeout = new ReactiveProperty<bool>(
                () => this.settings.EnablePlaylistTimeout, x => this.settings.EnablePlaylistTimeout = x,
                x => this.accessMode == Management.AccessMode.Administrator, typeof(AccessViolationException));

            this.LockLibraryRemoval = new ReactiveProperty<bool>(
                () => this.settings.LockLibraryRemoval, x => this.settings.LockLibraryRemoval = x,
                x => this.accessMode == Management.AccessMode.Administrator, typeof(AccessViolationException));

            this.LockPlaylistRemoval = new ReactiveProperty<bool>(
                () => this.settings.LockPlaylistRemoval, x => this.settings.LockPlaylistRemoval = x,
                x => this.accessMode == Management.AccessMode.Administrator, typeof(AccessViolationException));

            this.LockPlaylistSwitching = new ReactiveProperty<bool>(
                () => this.settings.LockPlaylistSwitching, x => this.settings.LockPlaylistSwitching = x,
                x => this.accessMode == Management.AccessMode.Administrator, typeof(AccessViolationException));

            this.LockPlayPause = new ReactiveProperty<bool>(
                () => this.settings.LockPlayPause, x => this.settings.LockPlayPause = x,
                x => this.accessMode == Management.AccessMode.Administrator, typeof(AccessViolationException));

            this.LockTime = new ReactiveProperty<bool>(
                () => this.settings.LockTime, x => this.settings.LockTime = x,
                x => this.accessMode == Management.AccessMode.Administrator, typeof(AccessViolationException));

            this.LockVolume = new ReactiveProperty<bool>(
                () => this.settings.LockVolume, x => this.settings.LockVolume = x,
                x => this.accessMode == Management.AccessMode.Administrator, typeof(AccessViolationException));
            /*
             * End boring, repeating glue code
             */

            this.CanChangeTime = this.AccessMode.CombineLatest(this.LockTime,
                (accessMode, lockTime) => accessMode == Management.AccessMode.Administrator || !lockTime);

            this.CanChangeVolume = this.AccessMode.CombineLatest(this.LockVolume,
                (accessMode, lockVolume) => accessMode == Management.AccessMode.Administrator || !lockVolume);

            this.CanSwitchPlaylist = this.AccessMode.CombineLatest(this.LockPlaylistSwitching,
                (accessMode, lockPlaylistSwitching) => accessMode == Management.AccessMode.Administrator || !lockPlaylistSwitching);

            this.songSourcePaths.Changed().Subscribe(x => this.UpdateSources());
        }

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

        public IObservable<bool> CanChangeTime { get; private set; }

        public IObservable<bool> CanChangeVolume { get; private set; }

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

        public IObservable<bool> CanSwitchPlaylist { get; private set; }

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
            get { return this.currentPlayer.FirstAsync().Wait() == null ? TimeSpan.Zero : this.currentPlayer.FirstAsync().Wait().CurrentTime; }
            set
            {
                this.ThrowIfNotAdmin();

                if (this.currentPlayer.FirstAsync().Wait() != null)
                {
                    this.currentPlayer.FirstAsync().Wait().CurrentTime = value;
                }
            }
        }

        public ReactiveProperty<bool> EnablePlaylistTimeout { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the administrator is created.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if the administrator is created; otherwise, <c>false</c>.
        /// </value>
        public bool IsAdministratorCreated { get; private set; }

        /// <summary>
        /// Gets the song that is currently loaded.
        /// </summary>
        public IObservable<Song> LoadedSong { get; private set; }

        public ReactiveProperty<bool> LockLibraryRemoval { get; private set; }

        public ReactiveProperty<bool> LockPlaylistRemoval { get; private set; }

        public ReactiveProperty<bool> LockPlaylistSwitching { get; private set; }

        public ReactiveProperty<bool> LockPlayPause { get; private set; }

        public ReactiveProperty<bool> LockTime { get; private set; }

        public ReactiveProperty<bool> LockVolume { get; private set; }

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

        public ReadOnlyObservableCollection<string> SongSourcePaths { get; private set; }

        /// <summary>
        /// Occurs when a song has started the playback.
        /// </summary>
        public IObservable<Unit> SongStarted
        {
            get { return this.songStarted.AsObservable(); }
        }

        /// <summary>
        /// Occurs when a song has been added to the library.
        /// </summary>
        public IObservable<Unit> SongsUpdated
        {
            get { return this.songsUpdated.AsObservable(); }
        }

        public bool StreamHighestYoutubeQuality
        {
            get { return this.settings.StreamHighestYoutubeQuality; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.StreamHighestYoutubeQuality = value;
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

        public IObservable<IVideoPlayerCallback> VideoPlayerCallback { get; private set; }

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

                if (this.currentPlayer.FirstAsync().Wait() != null)
                {
                    this.currentPlayer.FirstAsync().Wait().Volume = value;
                }
            }
        }

        public YoutubeStreamingQuality YoutubeStreamingQuality
        {
            get { return this.settings.YoutubeStreamingQuality; }
            set
            {
                this.ThrowIfNotAdmin();

                this.settings.YoutubeStreamingQuality = value;
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

        public void AddSongSourcePath(string path)
        {
            if (!Directory.Exists(path))
                throw new ArgumentException("Directory does't exist");

            this.songSourcePaths.Add(path);
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

            this.currentPlayer.FirstAsync().Wait().Play();
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
            if (this.currentPlayer.FirstAsync().Wait() != null)
            {
                this.currentPlayer.FirstAsync().Wait().Dispose();
            }

            this.driveWatcher.Dispose();

            this.abortUpdate = true;

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
            this.driveWatcher.DriveRemoved += (sender, args) => this.RemoveMissingSongsAsync();

            this.Load();
        }

        /// <summary>
        /// Pauses the currently loaded song.
        /// </summary>
        public void PauseSong()
        {
            if (this.LockPlayPause.Value && this.accessMode == Management.AccessMode.Party)
                throw new InvalidOperationException("Not allowed to play when in party mode.");

            this.currentPlayer.FirstAsync().Wait().Pause();
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

            if (!this.CurrentPlaylist.CanPlayPreviousSong.FirstAsync().Wait() || !this.CurrentPlaylist.CurrentSongIndex.Value.HasValue)
                throw new InvalidOperationException("The previous song couldn't be played.");

            this.PlaySong(this.CurrentPlaylist.CurrentSongIndex.Value.Value - 1);
        }

        /// <summary>
        /// Plays the song with the specified index in the playlist.
        /// </summary>
        /// <param name="playlistIndex">The index of the song in the playlist.</param>
        public void PlaySong(int playlistIndex)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            if (this.LockPlayPause.Value && this.accessMode == Management.AccessMode.Party)
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

            if (this.LockLibraryRemoval.Value && this.accessMode == Management.AccessMode.Party)
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

            if (this.LockPlaylistRemoval.Value && this.accessMode == Management.AccessMode.Party)
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

        public void RemoveSource(string source)
        {
            throw new NotImplementedException();
        }

        public void Save()
        {
            HashSet<LocalSong> casted;

            lock (this.songLock)
            {
                casted = new HashSet<LocalSong>(this.songs.Cast<LocalSong>());
            }

            // Clean up the songs that aren't in the library anymore
            // This happens when the user adds a song from a removable device to the playlist
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

            if (!this.CanSwitchPlaylist.FirstAsync().Wait())
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
            if (!this.CurrentPlaylist.CanPlayNextSong.FirstAsync().Wait())
            {
                this.CurrentPlaylist.CurrentSongIndex.Value = null;
            }

            else
            {
                this.InternPlayNextSong();
            }
        }

        private void HandleSongFinish(AudioPlayer audioPlayer)
        {
            if (!this.CurrentPlaylist.CanPlayNextSong.FirstAsync().Wait())
            {
                this.CurrentPlaylist.CurrentSongIndex.Value = null;
            }

            audioPlayer.Dispose();
            this.currentPlayer.OnNext(null);

            if (this.CurrentPlaylist.CanPlayNextSong.FirstAsync().Wait())
            {
                this.InternPlayNextSong();
            }
        }

        private void InternPlayNextSong()
        {
            if (!this.CurrentPlaylist.CanPlayNextSong.FirstAsync().Wait() || !this.CurrentPlaylist.CurrentSongIndex.Value.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            int nextIndex = this.CurrentPlaylist.CurrentSongIndex.Value.Value + 1;
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
                this.currentPlayingPlaylist.CurrentSongIndex.Value = null;
            }

            this.currentPlayingPlaylist = this.CurrentPlaylist;

            this.CurrentPlaylist.CurrentSongIndex.Value = playlistIndex;

            Song song = this.CurrentPlaylist[playlistIndex].Song;

            AudioPlayer audioPlayer;

            try
            {
                audioPlayer = this.RenewCurrentPlayer(song);
            }

            catch (AudioPlayerCreatingException)
            {
                this.HandleSongCorruption();

                return;
            }

            Task.Run(() =>
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
                    audioPlayer.Load();
                }

                catch (SongLoadException)
                {
                    song.IsCorrupted.Value = true;

                    this.HandleSongCorruption();

                    return;
                }

                try
                {
                    audioPlayer.Play();
                }

                catch (PlaybackException)
                {
                    song.IsCorrupted.Value = true;

                    this.HandleSongCorruption();

                    return;
                }

                this.songStarted.OnNext(Unit.Default);
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

            foreach (string path in this.libraryReader.ReadSources())
            {
                this.songSourcePaths.Add(path);
            }
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<int> indexes)
        {
            bool stopCurrentSong = playlist == this.CurrentPlaylist && indexes.Any(index => index == this.CurrentPlaylist.CurrentSongIndex.Value);

            playlist.RemoveSongs(indexes);

            if (stopCurrentSong)
            {
                this.currentPlayer.FirstAsync().Wait().Stop();
            }
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<Song> songList)
        {
            this.RemoveFromPlaylist(playlist, playlist.GetIndexes(songList));
        }

        private async Task RemoveMissingSongsAsync()
        {
            List<Song> currentSongs;

            lock (this.songLock)
            {
                currentSongs = this.songs.ToList();
            }

            await Task.Run(() =>
            {
                List<Song> removable = currentSongs
                    .Where(song => !File.Exists(song.OriginalPath))
                    .ToList();

                DisposeSongs(removable);

                foreach (Song song in removable)
                {
                    lock (this.songLock)
                    {
                        this.songs.Remove(song);
                    }
                }
            });
        }

        private AudioPlayer RenewCurrentPlayer(Song song)
        {
            AudioPlayer player = this.currentPlayer.FirstAsync().Wait();

            if (player != null)
            {
                player.Dispose();
            }

            AudioPlayer newAudioPlayer = song.CreateAudioPlayer();

            this.currentPlayer.OnNext(newAudioPlayer);

            // Set the volume after the currentPlayer is propagated so that an potential
            // IVideoPlayerCallback user can attach to the new AudioPlayer prior to that
            newAudioPlayer.Volume = this.Volume;

            return newAudioPlayer;
        }

        private void ThrowIfNotAdmin()
        {
            if (this.accessMode != Management.AccessMode.Administrator)
                throw new InvalidOperationException("Not in administrator mode.");
        }

        private async Task UpdateSources()
        {
            this.isUpdatingSources = true;

            await this.RemoveMissingSongsAsync();

            this.songsUpdated.OnNext(Unit.Default);

            var aggregator = new LocalSongFinderAggregator(this.songSourcePaths);

            aggregator.Songs.Subscribe(async song =>
            {
                if (this.abortUpdate)
                {
                    await aggregator.AbortAsync();
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
                    this.songsUpdated.OnNext(Unit.Default);
                }
            });

            await aggregator.ExecuteAsync();

            this.abortUpdate = false;
            this.isUpdatingSources = false;
        }
    }
}