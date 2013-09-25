using Rareform.Validation;
using System.Collections.Generic;
using System.IO;

namespace Espera.Core.Management
{
    public class LibraryFileReader : ILibraryReader
    {
        private readonly string sourcePath;

        public LibraryFileReader(string sourcePath)
        {
            if (sourcePath == null)
                Throw.ArgumentNullException(() => sourcePath);

            this.sourcePath = sourcePath;
        }

        public bool LibraryExists
        {
            get { return File.Exists(this.sourcePath); }
        }

        public IReadOnlyList<Playlist> ReadPlaylists()
        {
            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadPlaylists(sourceStream);
            }
        }

        public IReadOnlyList<LocalSong> ReadSongs()
        {
            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadSongs(sourceStream);
            }
        }

        public string ReadSongSourcePath()
        {
            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadSongSourcePath(sourceStream);
            }
        }
    }
}