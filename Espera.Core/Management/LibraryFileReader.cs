using Newtonsoft.Json;
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
            try
            {
                this.LoadToCache();

                return LibraryDeserializer.DeserializePlaylists(this.cache, this.songCache);
            }

            catch (Exception ex)
            {
                if (ex is JsonException || ex is IOException)
                {
                    throw new LibraryReadException("Failed to read playlists.", ex);
                }

                throw;
            }
        }

        public IReadOnlyList<LocalSong> ReadSongs()
        {
            IReadOnlyList<LocalSong> songs;

            try
            {
                this.LoadToCache();

                songs = LibraryDeserializer.DeserializeSongs(this.cache);
            }

            catch (Exception ex)
            {
                if (ex is JsonException || ex is IOException)
                {
                    throw new LibraryReadException("Failed to read songs.", ex);
                }

                throw;
            }

            this.songCache = songs;
            return songs;
        }

        public string ReadSongSourcePath()
        {
            try
            {
                this.LoadToCache();

                return LibraryDeserializer.DeserializeSongSourcePath(this.cache);
            }

            catch (Exception ex)
            {
                if (ex is JsonException || ex is IOException)
                {
                    throw new LibraryReadException("Failed to read song source path.", ex);
                }

                throw;
            }
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