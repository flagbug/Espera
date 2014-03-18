using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace Espera.Core.Management
{
    public class LibraryFileReader : ILibraryReader
    {
        private readonly string sourcePath;
        private JObject cache;
        private IReadOnlyList<LocalSong> songCache;

        public LibraryFileReader(string sourcePath)
        {
            if (sourcePath == null)
                throw new ArgumentNullException("sourcePath");

            this.sourcePath = sourcePath;
        }

        public bool LibraryExists
        {
            get { return File.Exists(this.sourcePath); }
        }

        public void InvalidateCache()
        {
            this.cache = null;
            this.songCache = null;
        }

        public IReadOnlyList<Playlist> ReadPlaylists()
        {
            this.LoadToCache();

            return LibraryReader.ReadPlaylists(this.cache, this.songCache);
        }

        public IReadOnlyList<LocalSong> ReadSongs()
        {
            this.LoadToCache();

            IReadOnlyList<LocalSong> songs = LibraryReader.ReadSongs(this.cache);
            this.songCache = songs;

            return songs;
        }

        public string ReadSongSourcePath()
        {
            this.LoadToCache();

            return LibraryReader.ReadSongSourcePath(this.cache);
        }

        private void LoadToCache()
        {
            if (cache != null)
                return;

            string json = File.ReadAllText(this.sourcePath);

            this.cache = JObject.Parse(json);
        }
    }
}