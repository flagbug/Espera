using System.Collections.Generic;

namespace Espera.Core.Management
{
    public interface ILibraryReader
    {
        bool LibraryExists { get; }

        IReadOnlyList<Playlist> ReadPlaylists();

        IReadOnlyList<LocalSong> ReadSongs();

        string ReadSongSourcePath();
    }
}