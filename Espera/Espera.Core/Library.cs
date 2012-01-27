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

        public event EventHandler<SongEventArgs> SongAdded;

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

        public IEnumerable<Song> Playlist
        {
            get
            {
                return playlist
                    .OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value);
            }
        }

        public float Volume
        {
            get { return this.audioPlayer.Volume; }
            set { this.audioPlayer.Volume = value; }
        }

        /// <summary>
        /// Gets the duration of the current played song.
        /// </summary>
        public TimeSpan TotalTime
        {
            get { return this.audioPlayer.TotalTime; }
        }

        /// <summary>
        /// Gets or sets the elapsed time of the current played song.
        /// </summary>
        public TimeSpan CurrentTime
        {
            get { return this.audioPlayer.CurrentTime; }
            set { this.audioPlayer.CurrentTime = value; }
        }

        public bool IsPlaying
        {
            get { return this.audioPlayer.PlaybackState == AudioPlayerState.Playing; }
        }

        public bool IsPaused
        {
            get { return this.audioPlayer.PlaybackState == AudioPlayerState.Paused; }
        }

        public Song CurrentSong
        {
            get { return this.audioPlayer.LoadedSong; }
        }

        public int CurrentSongPlaylistIndex { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Library"/> class.
        /// </summary>
        public Library()
        {
            this.audioPlayer = new AudioPlayer();
            this.songs = new HashSet<Song>();
            this.playlist = new Dictionary<int, Song>();
        }

        public void ContinueSong()
        {
            this.audioPlayer.Play();
        }

        public void PlaySong(int playlistIndex)
        {
            this.CurrentSongPlaylistIndex = playlistIndex;
            this.audioPlayer.Load(this.playlist[playlistIndex]);
            this.audioPlayer.Play();
        }

        public void PauseSong()
        {
            this.audioPlayer.Pause();
        }

        public void AddSongToPlaylist(Song song)
        {
            int newIndex = this.playlist.Keys.Count == 0 ? 0 : this.playlist.Keys.Max() + 1;

            this.playlist.Add(newIndex, song);
        }

        public Task AddLocalSongsAsync(string path)
        {
            return Task.Factory.StartNew(() => this.AddLocalSongs(path));
        }

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
    }
}