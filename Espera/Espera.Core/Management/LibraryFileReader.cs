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

        public IReadOnlyList<Playlist> ReadPlaylists()
        {
            if (!File.Exists(this.sourcePath))
                return new List<Playlist>();

            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadPlaylists(sourceStream, this.GetDriveType);
            }
        }

        public IReadOnlyList<LocalSong> ReadSongs()
        {
            if (!File.Exists(this.sourcePath))
                return new List<LocalSong>();

            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadSongs(sourceStream, this.GetDriveType);
            }
        }

        public string ReadSongSourcePath()
        {
            if (!File.Exists(this.sourcePath))
                return null;

            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadSongSourcePath(sourceStream);
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