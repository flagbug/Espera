using Rareform.Validation;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public IEnumerable<Playlist> ReadPlaylists()
        {
            if (!File.Exists(this.sourcePath))
                return Enumerable.Empty<Playlist>();

            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadPlaylists(sourceStream);
            }
        }

        public IEnumerable<LocalSong> ReadSongs()
        {
            if (!File.Exists(this.sourcePath))
                return Enumerable.Empty<LocalSong>();

            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadSongs(sourceStream);
            }
        }
    }
}