using System.Collections.Generic;

namespace Espera.Core.Management
{
    public interface ILibraryWriter
    {
        /// <summary>
        /// Writes the songs, playlist and the song directory to the save file.
        /// </summary>
        /// <exception cref="LibraryWriteException">
        /// An exception occured while writing the library.
        /// </exception>
        void Write(IEnumerable<LocalSong> songs, IEnumerable<Playlist> playlists, string songSourcePath);
    }
}