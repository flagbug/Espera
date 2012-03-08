using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Core.Library
{
    /// <summary>
    /// Represents a playlist where songs are stored with an associated index.
    /// </summary>
    public class Playlist : IEnumerable<Song>
    {
        private Dictionary<int, Song> playlist;
        private readonly ConcurrentQueue<Song> cachingQueue;
        private bool isCaching;
        private readonly int[] threads = { 0 };
        private readonly ManualResetEvent cachingHandle;
        private readonly ManualResetEvent bumpHandle;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="Playlist"/> class.
        /// </summary>
        public Playlist()
        {
            this.playlist = new Dictionary<int, Song>();
            this.cachingQueue = new ConcurrentQueue<Song>();
            this.cachingHandle = new ManualResetEvent(false);
            this.bumpHandle = new ManualResetEvent(false);
        }

        private void BumpCaching()
        {
            this.bumpHandle.Set();

            if (!this.isCaching)
            {
                this.isCaching = true;

                Song song;

                while (this.cachingQueue.TryDequeue(out song) && song != null)
                {
                    if (this.threads[0] == 3)
                    {
                        this.cachingHandle.WaitOne();
                    }

                    if (!song.IsCached)
                    {
                        this.threads[0]++;

                        if (threads[0] <= 3)
                        {
                            this.bumpHandle.Reset();
                        }

                        Task.Factory
                            .StartNew(song.LoadToCache)
                            .ContinueWith(task =>
                            {
                                threads[0]--;
                                this.cachingHandle.Set();
                                this.bumpHandle.Set();
                            });
                    }

                    if (threads[0] <= 3)
                    {
                        bumpHandle.WaitOne();
                    }
                }

                if (threads[0] == 0)
                {
                    this.isCaching = false;
                }
            }
        }

        /// <summary>
        /// Adds the specified song to end of the playlist.
        /// </summary>
        /// <param name="songList">The songs to add to the end of the playlist.</param>
        public void AddSongs(IEnumerable<Song> songList)
        {
            songList = songList.ToList(); // Avoid multiple enumeration

            foreach (Song song in songList)
            {
                this.cachingQueue.Enqueue(song);
            }

            Task.Factory.StartNew(this.BumpCaching);

            foreach (Song song in songList)
            {
                int index = this.playlist.Keys.Count == 0 ? 0 : this.playlist.Keys.Max() + 1;

                this.playlist.Add(index, song);
            }
        }

        /// <summary>
        /// Removes the songs with the specified indexes from the <see cref="Playlist"/>.
        /// </summary>
        /// <param name="indexes">The indexes of the songs to remove.</param>
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

        /// <summary>
        /// Gets the index in the playlist for each of the specified songs.
        /// </summary>
        /// <param name="songs">The songs.</param>
        /// <returns></returns>
        public IEnumerable<int> GetIndexes(IEnumerable<Song> songs)
        {
            return this.playlist
                .Where(entry => songs.Contains(entry.Value))
                .Select(entry => entry.Key);
        }

        /// <summary>
        /// Gets the <see cref="Espera.Core.Song"/> at the specified index.
        /// </summary>
        public Song this[int index]
        {
            get { return this.playlist[index]; }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<Song> GetEnumerator()
        {
            return this.playlist
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value)
                .GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
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