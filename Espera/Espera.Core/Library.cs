using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlagLib.Extensions;
using FlagLib.Reflection;

namespace Espera.Core
{
    public class Library : IDisposable
    {
        private readonly AudioPlayer audioPlayer;
        private readonly HashSet<Song> songs;
        private readonly Dictionary<int, Song> playlist;
        private readonly object songLocker = new object();
        private string password;

        /// <summary>
        /// Occurs when a song has been added to the library.
        /// </summary>
        public event EventHandler<SongEventArgs> SongAdded;

        /// <summary>
        /// Occurs when a song has started the playback.
        /// </summary>
        public event EventHandler SongStarted;

        /// <summary>
        /// Occurs when a song has finished the playback.
        /// </summary>
        public event EventHandler SongFinished;

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
            get { return this.audioPlayer.Volume; }
            set
            {
                if (this.AccessMode != AccessMode.Administrator)
                    throw new InvalidOperationException("The user is not in administrator mode.");

                this.audioPlayer.Volume = value;
            }
        }

        /// <summary>
        /// Gets the duration of the current song.
        /// </summary>
        public TimeSpan TotalTime
        {
            get { return this.audioPlayer.TotalTime; }
        }

        /// <summary>
        /// Gets or sets the current song's elapsed time.
        /// </summary>
        public TimeSpan CurrentTime
        {
            get { return this.audioPlayer.CurrentTime; }
            set
            {
                if (this.AccessMode != AccessMode.Administrator)
                    throw new InvalidOperationException("The user is not in administrator mode.");

                this.audioPlayer.CurrentTime = value;
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
            get { return this.audioPlayer.PlaybackState == AudioPlayerState.Playing; }
        }

        /// <summary>
        /// Gets a value indicating whether the playback is paused.
        /// </summary>
        /// <value>
        /// true if the playback is paused; otherwise, false.
        /// </value>
        public bool IsPaused
        {
            get { return this.audioPlayer.PlaybackState == AudioPlayerState.Paused; }
        }

        /// <summary>
        /// Gets the song that is currently loaded.
        /// </summary>
        public Song LoadedSong
        {
            get { return this.audioPlayer.LoadedSong; }
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
        public AccessMode AccessMode { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Library"/> class.
        /// </summary>
        public Library()
        {
            this.audioPlayer = new AudioPlayer();
            this.audioPlayer.SongFinished += (sender, e) => this.HandleSongFinish();

            this.songs = new HashSet<Song>();
            this.playlist = new Dictionary<int, Song>();
        }

        public void CreateAdmin(string adminPassword)
        {
            this.password = adminPassword;
        }

        public void ChangeToAdmin(string adminPassword)
        {
            if (this.password != adminPassword)
                throw new InvalidPasswordException("The password is not correct.");

            this.AccessMode = AccessMode.Administrator;
        }

        public void ChangeToUser()
        {
            this.AccessMode = AccessMode.User;
        }

        /// <summary>
        /// Continues the currently loaded song.
        /// </summary>
        public void ContinueSong()
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            this.audioPlayer.Play();
        }

        /// <summary>
        /// Plays the song with the specified index in the playlist.
        /// </summary>
        /// <param name="playlistIndex">The index of the song in the playlist.</param>
        public void PlaySong(int playlistIndex)
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            this.CurrentSongPlaylistIndex = playlistIndex;
            this.audioPlayer.Load(this.playlist[playlistIndex]);
            this.audioPlayer.Play();
            this.SongStarted.RaiseSafe(this, EventArgs.Empty);
        }

        /// <summary>
        /// Pauses the currently loaded song.
        /// </summary>
        public void PauseSong()
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            this.audioPlayer.Pause();
        }

        /// <summary>
        /// Plays the next song in the playlist.
        /// </summary>
        public void PlayNextSong()
        {
            if (this.AccessMode != AccessMode.Administrator)
                throw new InvalidOperationException("The user is not in administrator mode.");

            if (!this.CanPlayNextSong || !this.CurrentSongPlaylistIndex.HasValue)
                throw new InvalidOperationException("The next song couldn't be played.");

            this.PlaySong(this.CurrentSongPlaylistIndex.Value + 1);
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
        /// <param name="song">The song to add to the end of the playlist.</param>
        public void AddSongToPlaylist(Song song)
        {
            int newIndex = this.playlist.Keys.Count == 0 ? 0 : this.playlist.Keys.Max() + 1;

            this.playlist.Add(newIndex, song);
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
        /// Adds the song that are contained in the specified directory recursively to the library.
        /// </summary>
        /// <param name="path">The path of the directory to search.</param>
        public void AddLocalSongs(string path)
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
                    this.SongAdded.RaiseSafe(this, new SongEventArgs(e.Song));
                }
            };

            finder.Start();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.audioPlayer.Dispose();
        }

        private void HandleSongFinish()
        {
            this.SongFinished.RaiseSafe(this, EventArgs.Empty);

            if (this.CurrentSongPlaylistIndex != null)
            {
                int nextIndex = this.CurrentSongPlaylistIndex.Value + 1;

                if (this.playlist.ContainsKey(nextIndex))
                {
                    this.PlaySong(nextIndex);
                }
            }
        }
    }
}