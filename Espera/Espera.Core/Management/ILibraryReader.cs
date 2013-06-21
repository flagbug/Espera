using System.Collections.Generic;

namespace Espera.Core.Management
{
    public interface ILibraryReader
    {
        IReadOnlyList<Playlist> ReadPlaylists();

        IReadOnlyList<LocalSong> ReadSongs();

        string ReadSongSourcePath();
    }
}