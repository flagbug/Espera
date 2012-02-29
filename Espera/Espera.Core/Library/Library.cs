using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Espera.Core.Audio;
using Rareform.Extensions;
using Rareform.Reflection;

namespace Espera.Core.Library
{
    public class Library : IDisposable
    {
        private readonly HashSet<Song> songs;
        private Dictionary<int, Song> playlist;
        private readonly object songLocker;
        private string password;
        private AccessMode accessMode;
        private AudioPlayer currentPlayer;
        private float volume;

        /// <summary>
        /// Occurs when a song has been added to the library.
        /// </summary>
        public event EventHandler<LibraryFillEventArgs> SongAdded;

        /// <summary>
        /// Occurs when a song has started the playback.
        /// </summary>
        public event EventHandler SongStarted;

        /// <summary>
        /// Occurs when a song has finished the playback.
        /// </summary>
        public event EventHandler SongFinished;

        /// <summary>
        /// Occurs when <see cref="AccessMode"/> property has changed.
        /// </summary>
        public event EventHandler AccessModeChanged;

        /// <summary>
        /// Gets all songs that are currently in the library.
        /// </summary>
        public IEnumerable<Song> Songs
        {
            get
            {
                lock (songLocker)
                {
                    return this.songs;
                }
            }
        }

        /// <summary>
        /// Gets the songs that are in the playlist.
        /// </summary>
        public IEnumerable<Song> Playlist
        {
            get
            {
                return playlist
                    .OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value);
            }
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
                if (this.AccessMode != AccessMode.Administrator)
                    throw new InvalidOperationException("The user is not in administrator mode.");

                this.volume = value;

                if (this.currentPlayer != null)
                {
                    this.currentPlayer.Volume = value;
                }
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
        /// Gets or sets the current song's elapsed time.
        /// </summary>
        public TimeSpan CurrentTime
        {
            get { return this.currentPlayer == null ? TimeSpan.Zero : this.currentPlayer.CurrentTime; }
            set
            {
                if (this.AccessMode != AccessMode.Administrator)
                    throw new InvalidOperationException("The user is not in administrator mode.");

                this.currentPlayer.CurrentTime = value;
            }
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
        /// Gets the song that is currently loaded.
        /// </summary>
        public Song LoadedSong
        {
            get { return this.currentPlayer == null ? null : this.currentPlayer.LoadedSong; }
        }

        /// <summary>
        /// Gets the index of the current song in the playlist.
        /// </summary>
        /// <value>
        /// The index of the current song in the playlist.
        /// </value>
        public int? CurrentSongPlaylistIndex { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the next song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the next song in the playlist can be played; otherwise, false.
        /// </value>
        public bool CanPlayNextSong
        {
            get
            {
                return this.CurrentSongPlaylistIndex.HasValue &&
                       this.playlist.ContainsKey(this.CurrentSongPlaylistIndex.Value + 1);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the previous song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the previous song in the playlist can be played; otherwise, false.
        /// </value>
        public bool CanPlayPreviousSong
        {
            get
            {
                return this.CurrentSongPlaylistIndex.HasValue &&
                       this.playlist.ContainsKey(this.CurrentSongPlaylistIndex.Value - 1);
            }
        }

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

        public bool IsAdministratorCreated { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Library"/> class.
        /// </summary>
        public Library()
        {
            this.songLocker = new object();
            this.songs = new HashSet<Song>();
            this.playlist = new Dictionary<int, Song>();
            this.volume = 1.0f;
            this.AccessMode = AccessMode.Administrator; // We want implicit to be the administrator, till we change to user mode manually
        }

        public void CreateAdmin(string adminPassword)
        {
            if (adminPassword == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => adminPassword));

            if (this.IsAdministratorCreated)
                throw new InvalidOperationException("The administrator is already created.");

            this.password = adminPassword;
            this.IsAdministratorCreated = true;
        }

        public void ChangeToAdmin(string adminPassword)
        {
            if (adminPassword == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => adminPassword));

            if (this.password != adminPassword)
                throw new InvalidPasswordException("The password is not correct.");

            this.AccessMode = AccessMode.Administrator;
        }

        public void ChangeToUser()
        {
            this.AccessMode = AccessMode.User;
        }

        /// <summary>
        /// Plays the song with the specified index in the playlist.
        /// </summary>
        /// <param name="playlistIndex">The index of the song in the playlist.</param>
        public void PlaySong(int playlistIndex)
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            this.InternPlaySong(playlistIndex);
        }

        /// <summary>
        /// Continues the currently loaded song.
        /// </summary>
        public void ContinueSong()
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            this.currentPlayer.Play();
        }

        /// <summary>
        /// Pauses the currently loaded song.
        /// </summary>
        public void PauseSong()
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            this.currentPlayer.Pause();
        }

        /// <summary>
        /// Plays the next song in the playlist.
        /// </summary>
        public void PlayNextSong()
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            this.InternPlayNextSong();
        }

        /// <summary>
        /// Plays the previous song in the playlist.
        /// </summary>
        public void PlayPreviousSong()
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            if (!this.CanPlayPreviousSong || !this.CurrentSongPlaylistIndex.HasValue)
                throw new InvalidOperationException("The previous song couldn't be played.");

            this.PlaySong(this.CurrentSongPlaylistIndex.Value - 1);
        }

        /// <summary>
        /// Adds the specified song to end of the playlist.
        /// </summary>
        /// <param name="songList">The songs to add to the end of the playlist.</param>
        public void AddSongsToPlaylist(IEnumerable<Song> songList)
        {
            foreach (Song song in songList)
            {
                if (!song.IsCached)
                {
                    Task.Factory.StartNew(song.LoadToCache);
                }

                int newIndex = this.playlist.Keys.Count == 0 ? 0 : this.playlist.Keys.Max() + 1;

                this.playlist.Add(newIndex, song);
            }
        }

        public void RemoveFromPlaylist(IEnumerable<int> indexes)
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            foreach (int index in indexes)
            {
                if (index == this.CurrentSongPlaylistIndex)
                {
                    this.currentPlayer.Stop();
                    this.CurrentSongPlaylistIndex = null;
                }

                this.playlist.Remove(index);
            }

            this.RebuildPlaylist();
        }

        public void RemoveFromLibrary(IEnumerable<Song> songList)
        {
            foreach (Song song in songList)
            {
                this.songs.Remove(song);
            }

            var newPlaylist = playlist
                .Where(entry => this.songs.Contains(entry.Value))
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            playlist = newPlaylist;

            this.RebuildPlaylist();
        }

        /// <summary>
        /// Adds the song that are contained in the specified directory recursively in an asynchronous manner to the library.
        /// </summary>
        /// <param name="path">The path of the directory to search.</param>
        /// <returns>The <see cref="Task"/> that did the work.</returns>
        public Task AddLocalSongsAsync(string path)
        {
            return Task.Factory.StartNew(() => this.AddLocalSongs(path));
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
        }

        /// <summary>
        /// Rebuilds the playlist with new indexes.
        /// </summary>
        private void RebuildPlaylist()
        {
            var newPlaylist = new Dictionary<int, Song>();
            int index = 0;

            foreach (var entry in playlist.OrderBy(entry => entry.Key))
            {
                newPlaylist.Add(index, entry.Value);

                if (this.CurrentSongPlaylistIndex == entry.Key)
                {
                    this.CurrentSongPlaylistIndex = index;
                }

                index++;
            }

            this.playlist = newPlaylist;
        }

        /// <summary>
        /// Adds the song that are contained in the specified directory recursively to the library.
        /// </summary>
        /// <param name="path">The path of the directory to search.</param>
        private void AddLocalSongs(string path)
        {
            if (path == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => path));

            if (!Directory.Exists(path))
                throw new ArgumentException("The directory doesn't exist.", Reflector.GetMemberName(() => path));

            var finder = new LocalSongFinder(path);

            finder.SongFound += (sender, e) =>
            {
                bool added;

                lock (this.songLocker)
                {
                    added = this.songs.Add(e.Song);
                }

                if (added)
                {
                    this.SongAdded.RaiseSafe(this, new LibraryFillEventArgs(e.Song, finder.TagsProcessed, finder.CurrentTotalSongs));
                }
            };

            finder.Start();
        }

        private void InternPlaySong(int playlistIndex)
        {
            playlistIndex.ThrowIfLessThan(0, () => playlistIndex);

            this.CurrentSongPlaylistIndex = playlistIndex;

            Song song = this.playlist[playlistIndex];

            if (this.currentPlayer != null)
            {
                this.currentPlayer.Dispose();
            }

            this.currentPlayer = song.CreateAudioPlayer();

            this.currentPlayer.SongFinished += (sender, e) => this.HandleSongFinish();
            this.currentPlayer.Volume = this.Volume;

            this.currentPlayer.Load(song);
            this.currentPlayer.Play();
            this.SongStarted.RaiseSafe(this, EventArgs.Empty);
        }

        private void InternPlayNextSong()
        {
            if (!this.CanPlayNextSong || !this.CurrentSongPlaylistIndex.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            this.InternPlaySong(this.CurrentSongPlaylistIndex.Value + 1);
        }

        private void HandleSongFinish()
        {
            if (!this.CanPlayNextSong)
            {
                this.CurrentSongPlaylistIndex = null;
            }

            this.SongFinished.RaiseSafe(this, EventArgs.Empty);

            if (this.CanPlayNextSong)
            {
                this.InternPlayNextSong();
            }
        }
    }
}