using System.Collections.Generic;

namespace Espera.Core.Management
{
    public interface ILibraryWriter
    {
        void Write(IEnumerable<LocalSong> songs, IEnumerable<Playlist> playlists, IEnumerable<string> songSourcePaths);
    }
}