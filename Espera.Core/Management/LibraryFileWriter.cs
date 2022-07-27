using System;
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
            // Create a temporary file, write the library to it, then delete the original library
            // file and rename the new one. This ensures that there is a minimum possibility of
            // things going wrong, for example if the process is killed mid-writing.
            var tempFilePath = Path.Combine(Path.GetDirectoryName(targetPath), Guid.NewGuid().ToString());

            try
            {
                using (var targetStream = File.Create(tempFilePath))
                {
                    LibrarySerializer.Serialize(songs, playlists, songSourcePath, targetStream);
                }

                File.Delete(targetPath);
                File.Move(tempFilePath, targetPath);
            }

            catch (Exception ex)
            {
                if (ex is IOException || ex is UnauthorizedAccessException)
                    throw new LibraryWriteException("An error occured while writing the library file.", ex);

                throw;
            }
        }
    }
}