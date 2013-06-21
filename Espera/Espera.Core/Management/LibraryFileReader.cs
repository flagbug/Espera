using Rareform.Validation;
using System.Collections.Generic;
using System.IO;

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

        public bool LibraryExists
        {
            get { return File.Exists(this.sourcePath); }
        }

        public IReadOnlyList<Playlist> ReadPlaylists()
        {
            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadPlaylists(sourceStream, this.GetDriveType);
            }
        }

        public IReadOnlyList<LocalSong> ReadSongs()
        {
            using (FileStream sourceStream = File.OpenRead(this.sourcePath))
            {
                return LibraryReader.ReadSongs(sourceStream, this.GetDriveType);
            }
        }

        public string ReadSongSourcePath()
        {
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