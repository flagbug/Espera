using System.Collections.Generic;

namespace Espera.Core.Management
{
    public interface ILibraryReader
    {
        IEnumerable<Playlist> ReadPlaylists();

        IEnumerable<LocalSong> ReadSongs();
    }
}