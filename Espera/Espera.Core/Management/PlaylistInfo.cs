using System.Collections.Generic;

namespace Espera.Core.Management
{
    public sealed class PlaylistInfo
    {
        private readonly Playlist playlist;

        internal PlaylistInfo(Playlist playlist)
        {
            this.playlist = playlist;
        }

        public int? CurrentSongIndex
        {
            get { return this.playlist.CurrentSongIndex; }
        }

        public string Name
        {
            get { return this.playlist.Name; }
            set { this.playlist.Name = value; }
        }

        public IEnumerable<Song> Songs
        {
            get { return this.playlist; }
        }
    }
}