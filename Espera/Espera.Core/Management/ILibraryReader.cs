using System.Collections.Generic;

namespace Espera.Core.Management
{
    public interface ILibraryReader
    {
        bool LibraryExists { get; }

        void InvalidateCache();

        IReadOnlyList<Playlist> ReadPlaylists();

        IReadOnlyList<LocalSong> ReadSongs();

        string ReadSongSourcePath();
    }
}