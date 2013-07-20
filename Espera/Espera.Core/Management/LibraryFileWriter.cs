using Rareform.Validation;
using System.Collections.Generic;
using System.IO;

namespace Espera.Core.Management
{
    public class LibraryFileWriter : ILibraryWriter
    {
        private readonly string targetPath;

        public LibraryFileWriter(string targetPath)
        {
            if (targetPath == null)
                Throw.ArgumentNullException(() => targetPath);

            this.targetPath = targetPath;
        }

        public void Write(IEnumerable<LocalSong> songs, IEnumerable<Playlist> playlists, string songSourcePath)
        {
            using (FileStream targetStream = File.Create(this.targetPath))
            {
                LibraryWriter.Write(songs, playlists, songSourcePath, targetStream);
            }
        }
    }
}