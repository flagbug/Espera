using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public bool LibraryExists => File.Exists(sourcePath);

        public void InvalidateCache()
        {
            cache = null;
            songCache = null;
        }

        public IReadOnlyList<Playlist> ReadPlaylists()
        {
            try
            {
                LoadToCache();

                return LibraryDeserializer.DeserializePlaylists(cache, songCache);
            }

            catch (Exception ex)
            {
                if (ex is JsonException || ex is IOException)
                    throw new LibraryReadException("Failed to read playlists.", ex);

                throw;
            }
        }

        public IReadOnlyList<LocalSong> ReadSongs()
        {
            IReadOnlyList<LocalSong> songs;

            try
            {
                LoadToCache();

                songs = LibraryDeserializer.DeserializeSongs(cache);
            }

            catch (Exception ex)
            {
                if (ex is JsonException || ex is IOException)
                    throw new LibraryReadException("Failed to read songs.", ex);

                throw;
            }

            songCache = songs;
            return songs;
        }

        public string ReadSongSourcePath()
        {
            try
            {
                LoadToCache();

                return LibraryDeserializer.DeserializeSongSourcePath(cache);
            }

            catch (Exception ex)
            {
                if (ex is JsonException || ex is IOException)
                    throw new LibraryReadException("Failed to read song source path.", ex);

                throw;
            }
        }

        private void LoadToCache()
        {
            if (cache != null)
                return;

            var json = File.ReadAllText(sourcePath);

            cache = JObject.Parse(json);
        }
    }
}