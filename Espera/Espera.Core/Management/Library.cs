﻿using Akavache;
using Espera.Core.Audio;
using Espera.Core.Settings;
using Rareform.Extensions;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Espera.Core.Management
{
    public sealed class Library : IDisposable, IEnableLogger
    {
        private readonly AccessControl accessControl;
        private readonly AudioPlayer audioPlayer;
        private readonly Subject<Playlist> currentPlaylistChanged;
        private readonly IRemovableDriveWatcher driveWatcher;
        private readonly IFileSystem fileSystem;
        private readonly ILibraryReader libraryReader;
        private readonly ILibraryWriter libraryWriter;
        private readonly ObservableCollection<Playlist> playlists;
        private readonly ReadOnlyObservableCollection<Playlist> publicPlaylistWrapper;
        private readonly CoreSettings settings;
        private readonly object songLock;
        private readonly HashSet<LocalSong> songs;
        private readonly BehaviorSubject<string> songSourcePath;
        private readonly Subject<Unit> songStarted;
        private readonly Subject<Unit> songsUpdated;
        private Playlist currentPlayingPlaylist;
        private IDisposable currentSongFinderSubscription;
        private Playlist instantPlaylist;
        private DateTime lastSongAddTime;

        public Library(IRemovableDriveWatcher driveWatcher, ILibraryReader libraryReader, ILibraryWriter libraryWriter, CoreSettings settings, IFileSystem fileSystem)
        {
            this.driveWatcher = driveWatcher;
            this.libraryReader = libraryReader;
            this.libraryWriter = libraryWriter;
            this.settings = settings;
            this.fileSystem = fileSystem;

            this.accessControl = new AccessControl(settings);
            this.songLock = new object();
            this.songs = new HashSet<LocalSong>();
            this.playlists = new ObservableCollection<Playlist>();
            this.publicPlaylistWrapper = new ReadOnlyObservableCollection<Playlist>(this.playlists);
            this.currentPlaylistChanged = new Subject<Playlist>();
            this.CanPlayNextSong = this.currentPlaylistChanged.Select(x => x.CanPlayNextSong).Switch();
            this.CanPlayPreviousSong = this.currentPlaylistChanged.Select(x => x.CanPlayPreviousSong).Switch();
            this.songStarted = new Subject<Unit>();
            this.songSourcePath = new BehaviorSubject<string>(null);
            this.songsUpdated = new Subject<Unit>();
            this.audioPlayer = new AudioPlayer();

            this.LoadedSong = this.audioPlayer.LoadedSong;
            this.TotalTime = this.audioPlayer.TotalTime;
            this.PlaybackState = this.audioPlayer.PlaybackState;

            this.audioPlayer.PlaybackState.Where(p => p == AudioPlayerState.Finished)
                .CombineLatestValue(this.CanPlayNextSong, (state, canPlayNextSong) => canPlayNextSong)
                .Subscribe(canPlayNextSong => this.HandleSongFinishAsync(canPlayNextSong));

            this.CurrentTimeChanged = this.audioPlayer.CurrentTimeChanged;
        }

        public IAudioPlayerCallback AudioPlayerCallback
        {
            get { return this.audioPlayer; }
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
            get { return this.audioPlayer.CurrentTime; }
        }

        public IObservable<TimeSpan> CurrentTimeChanged { get; private set; }

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
        /// Returns an enumeration of playlists that implements <see cref="INotifyCollectionChanged"/>.
        /// </summary>
        public ReadOnlyObservableCollection<Playlist> Playlists
        {
            get { return this.publicPlaylistWrapper; }
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
        public IEnumerable<LocalSong> Songs
        {
            get
            {
                IEnumerable<LocalSong> tempSongs;

                lock (songLock)
                {
                    tempSongs = this.songs.ToList();
                }

                return tempSongs;
            }
        }

        public IObservable<string> SongSourcePath
        {
            get { return this.songSourcePath.AsObservable(); }
        }

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

        /// <summary>
        /// Gets the duration of the current song.
        /// </summary>
        public IObservable<TimeSpan> TotalTime { get; private set; }

        public float Volume
        {
            get { return this.settings.Volume; }
        }

        /// <summary>
        /// Adds a new playlist to the library and immediately sets it as the current playlist.
        /// </summary>
        /// <param name="name">The name of the playlist, It is required that no other playlist has this name.</param>
        /// <exception cref="InvalidOperationException">A playlist with the specified name already exists.</exception>
        public void AddAndSwitchToPlaylist(string name, Guid accessToken)
        {
            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlaylistSwitching);

            this.AddPlaylist(name, accessToken);
            this.SwitchToPlaylist(this.GetPlaylistByName(name), accessToken);
        }

        /// <summary>
        /// Adds a new playlist with the specified name to the library.
        /// </summary>
        /// <param name="name">The name of the playlist. It is required that no other playlist has this name.</param>
        /// <exception cref="InvalidOperationException">A playlist with the specified name already exists.</exception>
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
        /// Adds the specified song to the end of the playlist.
        /// This method is only available in administrator mode.
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
            this.audioPlayer.Dispose();

            this.driveWatcher.Dispose();

            if (this.currentSongFinderSubscription != null)
            {
                this.currentSongFinderSubscription.Dispose();
            }
        }

        public Playlist GetPlaylistByName(string playlistName)
        {
            if (playlistName == null)
                Throw.ArgumentNullException(() => playlistName);

            return this.playlists.FirstOrDefault(playlist => playlist.Name == playlistName);
        }

        public void Initialize()
        {
            this.driveWatcher.Initialize();

            IObservable<Unit> update = this.settings.WhenAnyValue(x => x.SongSourceUpdateInterval)
                .Select(Observable.Interval)
                .Switch()
                .Select(_ => Unit.Default)
                .Merge(this.driveWatcher.DriveRemoved)
                .StartWith(Unit.Default);

            update.CombineLatest(this.songSourcePath, (_, path) => path)
                .Where(path => !String.IsNullOrEmpty(path))
                .Do(_ => this.Log().Info("Triggering library update."))
                .Subscribe(path => this.UpdateSongsAsync(path));

            if (this.libraryReader.LibraryExists)
            {
                this.Load();
            }
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

            if (this.instantPlaylist != null)
            {
                this.playlists.Remove(instantPlaylist);
            }

            string instantPlaylistName = Guid.NewGuid().ToString();
            this.instantPlaylist = new Playlist(instantPlaylistName, true);
            this.instantPlaylist.AddSongs(songList.ToList());

            this.playlists.Add(this.instantPlaylist);
            this.SwitchToPlaylist(this.instantPlaylist, accessToken);

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

            if (!this.CurrentPlaylist.CurrentSongIndex.Value.HasValue)
                throw new InvalidOperationException("The previous song can't be played as there is no current playlist index.");

            await this.PlaySongAsync(this.CurrentPlaylist.CurrentSongIndex.Value.Value - 1, accessToken);
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

        /// <summary>
        /// Removes the songs with the specified indexes from the playlist.
        /// </summary>
        /// <param name="indexes">The indexes of the songs to remove from the playlist.</param>
        public void RemoveFromPlaylist(IEnumerable<int> indexes, Guid accessToken)
        {
            if (indexes == null)
                Throw.ArgumentNullException(() => indexes);

            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlaylistRemoval);

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

            this.libraryWriter.Write(casted, this.playlists.Where(playlist => !playlist.IsTemporary), this.songSourcePath.FirstAsync().Wait());
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
            this.audioPlayer.Volume = volume;
        }

        public void ShufflePlaylist()
        {
            this.CurrentPlaylist.Shuffle();
        }

        public void SwitchToPlaylist(Playlist playlist, Guid accessToken)
        {
            if (playlist == null)
                Throw.ArgumentNullException(() => playlist);

            this.accessControl.VerifyAccess(accessToken, this.settings.LockPlaylistSwitching);

            this.CurrentPlaylist = playlist;
            this.currentPlaylistChanged.OnNext(playlist);
        }

        private async Task HandleSongCorruptionAsync()
        {
            if (!await this.CurrentPlaylist.CanPlayNextSong.FirstAsync())
            {
                this.CurrentPlaylist.CurrentSongIndex.Value = null;
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
                this.CurrentPlaylist.CurrentSongIndex.Value = null;
            }

            if (canPlayNextSong)
            {
                await this.InternPlayNextSongAsync();
            }
        }

        private async Task InternPlayNextSongAsync()
        {
            if (!await this.CurrentPlaylist.CanPlayNextSong.FirstAsync() || !this.CurrentPlaylist.CurrentSongIndex.Value.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            int nextIndex = this.CurrentPlaylist.CurrentSongIndex.Value.Value + 1;

            await this.InternPlaySongAsync(nextIndex);
        }

        private async Task InternPlaySongAsync(int playlistIndex)
        {
            if (playlistIndex < 0)
                Throw.ArgumentOutOfRangeException(() => playlistIndex, 0);

            if (this.currentPlayingPlaylist != null && this.currentPlayingPlaylist != this.CurrentPlaylist)
            {
                this.currentPlayingPlaylist.CurrentSongIndex.Value = null;
            }

            this.currentPlayingPlaylist = this.CurrentPlaylist;

            this.CurrentPlaylist.CurrentSongIndex.Value = playlistIndex;

            this.audioPlayer.Volume = this.Volume;

            Song song = this.CurrentPlaylist[playlistIndex].Song;

            try
            {
                await song.PrepareAsync(this.settings.StreamHighestYoutubeQuality ? YoutubeStreamingQuality.High : this.settings.YoutubeStreamingQuality);
            }

            catch (SongPreparationException)
            {
                this.HandleSongCorruptionAsync();

                return;
            }

            try
            {
                await this.audioPlayer.LoadAsync(song);
            }

            catch (SongLoadException)
            {
                song.IsCorrupted.Value = true;

                this.HandleSongCorruptionAsync();

                return;
            }

            try
            {
                await this.audioPlayer.PlayAsync();
            }

            catch (PlaybackException)
            {
                song.IsCorrupted.Value = true;

                this.HandleSongCorruptionAsync();

                return;
            }

            this.songStarted.OnNext(Unit.Default);
        }

        private void Load()
        {
            IEnumerable<LocalSong> savedSongs = this.libraryReader.ReadSongs();

            foreach (LocalSong song in savedSongs)
            {
                this.songs.Add(song);
            }

            IEnumerable<Playlist> savedPlaylists = this.libraryReader.ReadPlaylists();

            foreach (Playlist playlist in savedPlaylists)
            {
                this.playlists.Add(playlist);
            }

            this.songSourcePath.OnNext(this.libraryReader.ReadSongSourcePath());
        }

        /// <summary>
        /// Removes the specified songs from the library.
        /// </summary>
        /// <param name="songList">The list of the songs to remove from the library.</param>
        private void RemoveFromLibrary(IEnumerable<Song> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            List<Song> enumerable = songList.ToList();

            foreach (string key in enumerable.Cast<LocalSong>().Select(x => x.ArtworkKey.FirstAsync().Wait()).Where(x => x != null))
            {
                BlobCache.LocalMachine.Invalidate(key);
            }

            this.playlists.ForEach(playlist => this.RemoveFromPlaylist(playlist, enumerable));

            lock (this.songLock)
            {
                foreach (LocalSong song in enumerable)
                {
                    this.songs.Remove(song);
                }
            }
        }

        private void RemoveFromPlaylist(Playlist playlist, IEnumerable<int> indexes)
        {
            bool stopCurrentSong = playlist == this.CurrentPlaylist && indexes.Any(index => index == this.CurrentPlaylist.CurrentSongIndex.Value);

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
            List<LocalSong> currentSongs;

            lock (this.songLock)
            {
                currentSongs = this.songs.ToList();
            }

            List<LocalSong> notInAnySongSource = currentSongs
                .Where(song => !song.OriginalPath.StartsWith(currentPath))
                .ToList();

            HashSet<Song> removable = null;

            await Task.Run(() =>
            {
                List<LocalSong> nonExistant = currentSongs
                    .Where(song => !this.fileSystem.File.Exists(song.OriginalPath))
                    .ToList();

                removable = new HashSet<Song>(notInAnySongSource.Concat(nonExistant));
            });

            this.RemoveFromLibrary(removable);
        }

        private async Task UpdateSongsAsync(string path)
        {
            if (this.currentSongFinderSubscription != null)
            {
                this.currentSongFinderSubscription.Dispose();
                this.currentSongFinderSubscription = null;
            }

            await this.RemoveMissingSongsAsync(path);

            this.songsUpdated.OnNext(Unit.Default);

            var artworkLookup = new HashSet<string>(this.Songs.Cast<LocalSong>().Select(x => x.ArtworkKey.FirstAsync().Wait()).Where(x => x != null));

            var songFinder = new LocalSongFinder(path);

            this.currentSongFinderSubscription = songFinder.GetSongs()
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe(t =>
                {
                    LocalSong song = t.Item1;

                    bool added;

                    lock (this.songLock)
                    {
                        added = this.songs.Add(song);
                    }

                    if (added)
                    {
                        byte[] artworkData = t.Item2;

                        if (artworkData != null)
                        {
                            byte[] hash = MD5.Create().ComputeHash(artworkData);

                            string artworkKey = "artwork-" + BitConverter.ToString(hash).Replace("-", "").ToLower();

                            if (artworkLookup.Add(artworkKey))
                            {
                                this.Log().Info("Adding new artwork {0} of {1} to the BlobCache", artworkKey, song);

                                BlobCache.LocalMachine.Insert(artworkKey, artworkData)
                                    .Do(_ => this.Log().Debug("Added artwork {0} to the BlobCache", artworkKey))
                                    .Subscribe(x => song.NotifyArtworkStored(artworkKey));
                            }

                            else
                            {
                                song.NotifyArtworkStored(artworkKey);
                            }
                        }

                        this.songsUpdated.OnNext(Unit.Default);
                    }
                });
        }
    }
}