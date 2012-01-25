using System;
using System.Collections.Generic;
using System.IO;
using FlagLib.Extensions;
using FlagLib.Reflection;

namespace FlagTunes.Core
{
    public class Library
    {
        private readonly HashSet<Song> songs;

        public event EventHandler<SongEventArgs> SongAdded;

        public IEnumerable<Song> Songs
        {
            get { return this.songs; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Library"/> class.
        /// </summary>
        public Library()
        {
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