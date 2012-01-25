using System;
using System.Collections.Generic;
using System.IO;
using FlagLib.Reflection;

namespace FlagTunes.Core
{
    public class Library
    {
        private readonly HashSet<Song> songs;

        /// <summary>
        /// Initializes a new instance of the <see cref="Library"/> class.
        /// </summary>
        public Library()
        {
            this.songs = new HashSet<Song>();
        }

        public void AddSongsFromLocal(string path, Action<Song> updateCallback)
        {
            if (path == null)
                throw new ArgumentNullException(Reflector.GetMemberName(() => path));

            if (!Directory.Exists(path))
                throw new ArgumentException("The directory doesn't exists.", Reflector.GetMemberName(() => path));

            var finder = new SongFinder(path);

            finder.SongFound += (sender, e) =>
            {
                bool added = this.songs.Add(e.Song);

                if (added && updateCallback != null)
                {
                    updateCallback(e.Song);
                }
            };

            finder.Start();
        }
    }
}