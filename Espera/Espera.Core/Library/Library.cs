using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Espera.Core.Audio;
using Rareform.Extensions;
using Rareform.Reflection;

namespace Espera.Core.Library
{
    public class Library : IDisposable
    {
        private readonly HashSet<Song> songs;
        private readonly Playlist playlist;
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
            get { return this.playlist; }
        }

        /// <summary>
        /// Gets the index of the currently played song in the playlist.
        /// </summary>
        /// <value>
        /// The index of the currently played song in the playlist.
        /// </value>
        public int? CurrentSongIndex
        {
            get { return this.playlist.CurrentSongIndex; }
        }

        public bool CanPlayNextSong
        {
            get { return this.playlist.CanPlayNextSong; }
        }

        public bool CanPlayPreviousSong
        {
            get { return this.playlist.CanPlayPreviousSong; }
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

        /// <summary>
        /// Gets a value indicating whether the administrator is created.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if the administrator is created; otherwise, <c>false</c>.
        /// </value>
        public bool IsAdministratorCreated { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Library"/> class.
        /// </summary>
        public Library()
        {
            this.songLocker = new object();
            this.songs = new HashSet<Song>();
            this.playlist = new Playlist();
            this.volume = 1.0f;
            this.AccessMode = AccessMode.Administrator; // We want implicit to be the administrator, till we change to user mode manually
        }

        /// <summary>
        /// Creates the administrator with the specified password.
        /// </summary>
        /// <param name="adminPassword">The administrator password.</param>
        public void CreateAdmin(string adminPassword)
        {
            if (adminPassword == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => adminPassword));

            if (this.IsAdministratorCreated)
                throw new InvalidOperationException("The administrator is already created.");

            this.password = adminPassword;
            this.IsAdministratorCreated = true;
        }

        /// <summary>
        /// Logs the administrator with the specified password in.
        /// </summary>
        /// <param name="adminPassword">The administrator password.</param>
        public void ChangeToAdmin(string adminPassword)
        {
            if (adminPassword == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => adminPassword));

            if (this.password != adminPassword)
                throw new InvalidPasswordException("The password is not correct.");

            this.AccessMode = AccessMode.Administrator;
        }

        /// <summary>
        /// Changes the access mode to user mode.
        /// </summary>
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

            if (!this.playlist.CanPlayPreviousSong || !this.playlist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The previous song couldn't be played.");

            this.PlaySong(this.playlist.CurrentSongIndex.Value - 1);
        }

        /// <summary>
        /// Adds the specified song to end of the playlist.
        /// </summary>
        /// <param name="songList">The songs to add to the end of the playlist.</param>
        public void AddSongsToPlaylist(IEnumerable<Song> songList)
        {
            this.playlist.AddSongs(songList);
        }

        /// <summary>
        /// Removes the songs with the specified indexes from the playlist.
        /// </summary>
        /// <param name="indexes">The indexes of the songs to remove from the playlist.</param>
        public void RemoveFromPlaylist(IEnumerable<int> indexes)
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            indexes = indexes.ToList(); // Avoid multiple enumeration

            bool stopCurrentSong = indexes.Any(index => index == this.playlist.CurrentSongIndex);

            this.playlist.RemoveSongs(indexes);

            if (stopCurrentSong)
            {
                this.currentPlayer.Stop();
                this.SongFinished.RaiseSafe(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Removes the specified songs from the library.
        /// </summary>
        /// <param name="songList">The list of the songs to remove from the library.</param>
        public void RemoveFromLibrary(IEnumerable<Song> songList)
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            songList = songList.ToList(); // Avoid multiple enumeration

            foreach (Song song in songList)
            {
                this.songs.Remove(song);
            }

            this.RemoveFromPlaylist(this.playlist.Getindexes(songList));
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

            foreach (Song song in songs)
            {
                if (song.IsCached)
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

            this.playlist.CurrentSongIndex = playlistIndex;

            Song song = this.playlist[playlistIndex];

            if (this.currentPlayer != null)
            {
                this.currentPlayer.Dispose();
            }

            this.currentPlayer = song.CreateAudioPlayer();

            this.currentPlayer.SongFinished += (sender, e) => this.HandleSongFinish();
            this.currentPlayer.Volume = this.Volume;

            Task.Factory.StartNew(() =>
            {
                // Wait till the song is cached
                while (!song.IsCached)
                {
                    Thread.Sleep(250);
                }

                this.currentPlayer.Load(song);
                this.currentPlayer.Play();

                this.SongStarted.RaiseSafe(this, EventArgs.Empty);
            });
        }

        private void InternPlayNextSong()
        {
            if (!this.playlist.CanPlayNextSong || !this.playlist.CurrentSongIndex.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            this.InternPlaySong(this.playlist.CurrentSongIndex.Value + 1);
        }

        private void HandleSongFinish()
        {
            if (!this.playlist.CanPlayNextSong)
            {
                this.playlist.CurrentSongIndex = null;
            }

            this.SongFinished.RaiseSafe(this, EventArgs.Empty);

            if (this.playlist.CanPlayNextSong)
            {
                this.InternPlayNextSong();
            }
        }
    }
}