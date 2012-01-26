using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FlagLib.Extensions;
using FlagLib.Reflection;

namespace Espera.Core
{
    public class Library
    {
        private readonly AudioPlayer audioPlayer;
        private readonly HashSet<Song> songs;
        private readonly List<Song> playlist;
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
            get { return playlist; }
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
        /// Gets the elapsed time of the current played song.
        /// </summary>
        public TimeSpan CurrentTime
        {
            get { return this.audioPlayer.CurrentTime; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Library"/> class.
        /// </summary>
        public Library()
        {
            this.audioPlayer = new AudioPlayer();
            this.songs = new HashSet<Song>();
            this.playlist = new List<Song>();
        }

        public void AddSongToPlaylist(Song song)
        {
            this.playlist.Add(song);
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
    }
}