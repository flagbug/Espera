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

        public void Write(IEnumerable<LocalSong> songs, IEnumerable<PlaylistInfo> playlists)
        {
            using (FileStream targetStream = File.OpenWrite(this.targetPath))
            {
                LibraryWriter.Write(songs, playlists, targetStream);
            }
        }
    }
}