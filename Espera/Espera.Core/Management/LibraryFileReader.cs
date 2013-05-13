using Rareform.Validation;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Espera.Core.Management
{
    public class LibraryFileReader : ILibraryReader
    {
        private readonly Dictionary<string, DriveType> driveTypeCache;
        private readonly string sourcePath;

        public LibraryFileReader(string sourcePath)
        {
            if (sourcePath == null)
                Throw.ArgumentNullException(() => sourcePath);

            this.sourcePath = sourcePath;
            this.driveTypeCache = new Dictionary<string, DriveType>();
        }

        public IEnumerable<Playlist> ReadPlaylists()
        {
            if (!File.Exists(this.sourcePath))
                return Enumerable.Empty<Playlist>();

            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadPlaylists(sourceStream, this.GetDriveType);
            }
        }

        public IEnumerable<LocalSong> ReadSongs()
        {
            if (!File.Exists(this.sourcePath))
                return Enumerable.Empty<LocalSong>();

            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadSongs(sourceStream, this.GetDriveType);
            }
        }

        public IEnumerable<string> ReadSongSourcePaths()
        {
            if (!File.Exists(this.sourcePath))
                return Enumerable.Empty<string>();

            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadSongSourcePaths(sourceStream);
            }
        }

        private DriveType GetDriveType(string path)
        {
            string root = Path.GetPathRoot(path);

            DriveType driveType;

            if (this.driveTypeCache.TryGetValue(root, out driveType))
            {
                return driveType;
            }

            driveType = new DriveInfo(root).DriveType;

            this.driveTypeCache.Add(root, driveType);

            return driveType;
        }
    }
}