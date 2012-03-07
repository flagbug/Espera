using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Espera.Core.Library
{
    public class Playlist : IEnumerable<Song>
    {
        private Dictionary<int, Song> playlist;
        private readonly object cacheSyncLock;

        /// <summary>
        /// Gets the index of the currently played song in the playlist.
        /// </summary>
        /// <value>
        /// The index of the currently played song in the playlist.
        /// </value>
        public int? CurrentSongIndex { get; set; }

        /// <summary>
        /// Gets a value indicating whether the next song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the next song in the playlist can be played; otherwise, false.
        /// </value>
        public bool CanPlayNextSong
        {
            get { return this.CurrentSongIndex.HasValue && this.playlist.ContainsKey(this.CurrentSongIndex.Value + 1); }
        }

        /// <summary>
        /// Gets a value indicating whether the previous song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the previous song in the playlist can be played; otherwise, false.
        /// </value>
        public bool CanPlayPreviousSong
        {
            get { return this.CurrentSongIndex.HasValue && this.playlist.ContainsKey(this.CurrentSongIndex.Value - 1); }
        }

        public Playlist()
        {
            this.playlist = new Dictionary<int, Song>();
            this.cacheSyncLock = new object();
        }

        /// <summary>
        /// Adds the specified song to end of the playlist.
        /// </summary>
        /// <param name="songList">The songs to add to the end of the playlist.</param>
        public void AddSongs(IEnumerable<Song> songList)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = 3 };

            Task.Factory.StartNew(() =>
            {
                // This lock synchronizes the case that multiple calls of the AddSongs method occur,
                // before the first sequence of songs is cached completely
                lock (cacheSyncLock)
                {
                    Parallel.ForEach(songList.Where(song => !song.IsCached), options, song => song.LoadToCache());
                }
            });

            foreach (Song song in songList)
            {
                int newIndex = this.playlist.Keys.Count == 0 ? 0 : this.playlist.Keys.Max() + 1;

                this.playlist.Add(newIndex, song);
            }
        }

        public void RemoveSongs(IEnumerable<int> indexes)
        {
            foreach (int index in indexes)
            {
                if (index == this.CurrentSongIndex)
                {
                    this.CurrentSongIndex = null;
                }

                this.playlist.Remove(index);
            }

            this.Rebuild();
        }

        public IEnumerable<int> GetIndexes(IEnumerable<Song> songs)
        {
            return this.playlist
                .Where(entry => songs.Contains(entry.Value))
                .Select(entry => entry.Key);
        }

        public Song this[int index]
        {
            get { return this.playlist[index]; }
        }

        public IEnumerator<Song> GetEnumerator()
        {
            return this.playlist
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value)
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Rebuilds the playlist with new indexes.
        /// </summary>
        private void Rebuild()
        {
            var newPlaylist = new Dictionary<int, Song>();
            int index = 0;

            foreach (var entry in playlist.OrderBy(entry => entry.Key))
            {
                newPlaylist.Add(index, entry.Value);

                if (this.CurrentSongIndex == entry.Key)
                {
                    this.CurrentSongIndex = index;
                }

                index++;
            }

            this.playlist = newPlaylist;
        }
    }
}