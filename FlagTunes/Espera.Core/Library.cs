using System;
using System.Collections.Generic;
using System.IO;
using FlagLib.Extensions;
using FlagLib.Reflection;

namespace Espera.Core
{
    public class Library
    {
        private readonly AudioPlayer audioPlayer;
        private readonly HashSet<Song> songs;

        public event EventHandler<SongEventArgs> SongAdded;

        public IEnumerable<Song> Songs
        {
            get { return this.songs; }
        }

        public TimeSpan TotalTime
        {
            get { return this.audioPlayer.TotalTime; }
        }

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
                if (this.songs.Add(e.Song))
                {
                    this.SongAdded.RaiseSafe(this, new SongEventArgs(e.Song));
                }
            };

            finder.Start();
        }
    }
}