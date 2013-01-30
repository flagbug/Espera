using Rareform.Collections;
using Rareform.Reflection;
using Rareform.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Espera.Core.Management
{
    /// <summary>
    /// Represents a playlist where songs are stored with an associated index.
    /// </summary>
    public sealed class Playlist : IEnumerable<PlaylistEntry>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private readonly BehaviorSubject<int?> currentSongIndex;
        private string name;
        private readonly ObservableList<PlaylistEntry> playlist;

        internal Playlist(string name, bool isTemporary = false)
        {
            this.Name = name;
            this.IsTemporary = isTemporary;
            
            this.playlist = new ObservableList<PlaylistEntry>();
            this.currentSongIndex = new BehaviorSubject<int?>(null);
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add { this.playlist.CollectionChanged += value; }
            remove { this.playlist.CollectionChanged -= value; }
        }

        public event PropertyChangedEventHandler PropertyChanged
        {
            add { ((INotifyPropertyChanged)this.playlist).PropertyChanged += value; }
            remove { ((INotifyPropertyChanged)this.playlist).PropertyChanged -= value; }
        }

        /// <summary>
        /// Gets a value indicating whether the next song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the next song in the playlist can be played; otherwise, false.
        /// </value>
        public bool CanPlayNextSong
        {
            get { return this.CurrentSongIndex.HasValue && this.ContainsIndex(this.CurrentSongIndex.Value + 1); }
        }

        /// <summary>
        /// Gets a value indicating whether the previous song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the previous song in the playlist can be played; otherwise, false.
        /// </value>
        public bool CanPlayPreviousSong
        {
            get { return this.CurrentSongIndex.HasValue && this.ContainsIndex(this.CurrentSongIndex.Value - 1); }
        }

        /// <summary>
        /// Gets the index of the currently played song in the playlist.
        /// </summary>
        /// <value>
        /// The index of the currently played song in the playlist. <c>null</c>, if no song is currently played.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">The value is not in the range of the playlist's indexes.</exception>
        public int? CurrentSongIndex
        {
            get { return this.currentSongIndex.First(); }
            internal set
            {
                if (value != null && !this.ContainsIndex(value.Value))
                    Throw.ArgumentOutOfRangeException(() => value);

                this.currentSongIndex.OnNext(value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this playlist is temporary and used for instant-playing.
        /// This means that this playlist isn't saved to the harddrive when closing the application.
        /// </summary>
        public bool IsTemporary { get; private set; }
        public IObservable<int?> CurrentSongIndexChanged
        {
            get { return this.currentSongIndex.AsObservable(); }
        }


        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.IsTemporary)
                    throw new InvalidOperationException("Cannot change the name of a temporary playlist.");

                this.name = value;
            }
        }

        public PlaylistEntry this[int index]
        {
            get
            {
                if (index < 0)
                    Throw.ArgumentOutOfRangeException(() => index, 0);

                int maxIndex = this.playlist.Count;

                if (index > maxIndex)
                    Throw.ArgumentOutOfRangeException(() => index, maxIndex);

                return this.playlist[index];
            }
        }

        /// <summary>
        /// Gets a value indicating whether there exists a song at the specified index.
        /// </summary>
        /// <param name="songIndex">The index to look for.</param>
        /// <returns>True, if there exists a song at the specified index; otherwise, false.</returns>
        public bool ContainsIndex(int songIndex)
        {
            return this.playlist.Any(entry => entry.Index == songIndex);
        }

        public IEnumerator<PlaylistEntry> GetEnumerator()
        {
            return this.playlist.GetEnumerator();
        }

        /// <summary>
        /// Gets all indexes of the specified songs.
        /// </summary>
        public IEnumerable<int> GetIndexes(IEnumerable<Song> songs)
        {
            return this.playlist
                .Where(entry => songs.Contains(entry.Song))
                .Select(entry => entry.Index)
                .ToList();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Adds the specified songs to end of the playlist.
        /// </summary>
        /// <param name="songList">The songs to add to the end of the playlist.</param>
        internal void AddSongs(IEnumerable<Song> songList)
        {
            if (songList == null)
                Throw.ArgumentNullException(() => songList);

            var itemsToAdd = new List<PlaylistEntry>();

            int index = this.playlist.Count;

            foreach (Song song in songList)
            {
                if (song.HasToCache && !song.IsCaching)
                {
                    GlobalSongCacheQueue.Instance.Enqueue(song);
                }

                itemsToAdd.Add(new PlaylistEntry(index++, song));
            }

            this.playlist.AddRange(itemsToAdd);
        }

        /// <summary>
        /// Inserts a song from a specified index to a other index in the playlist and moves all songs in between these indexes one index back.
        /// </summary>
        /// <param name="fromIndex">The index of the song to move.</param>
        /// <param name="toIndex">To index to insert the song.</param>
        internal void InsertMove(int fromIndex, int toIndex)
        {
            if (fromIndex < 0)
                Throw.ArgumentOutOfRangeException(() => fromIndex, 0);

            if (toIndex < 0)
                Throw.ArgumentOutOfRangeException(() => toIndex, 0);

            if (toIndex >= fromIndex)
                Throw.ArgumentException(
                    String.Format("{0} has to be smaller than {1}",
                    Reflector.GetMemberName(() => toIndex), Reflector.GetMemberName(() => fromIndex)),
                    () => toIndex);

            PlaylistEntry from = this[fromIndex];

            for (int i = fromIndex; i > toIndex; i--)
            {
                this.playlist[i].Index = i - 1;
                this.playlist[i] = this[i - 1];
            }

            from.Index = toIndex;
            this.playlist[toIndex] = from;
        }

        /// <summary>
        /// Removes the songs with the specified indexes from the <see cref="Playlist"/>.
        /// </summary>
        /// <param name="indexes">The indexes of the songs to remove.</param>
        internal void RemoveSongs(IEnumerable<int> indexes)
        {
            if (indexes == null)
                Throw.ArgumentNullException(() => indexes);

            // Use a HashSet for better lookup performance
            var indexList = new HashSet<int>(indexes);

            if (this.CurrentSongIndex.HasValue && indexList.Contains(this.CurrentSongIndex.Value))
            {
                this.CurrentSongIndex = null;
            }

            this.playlist.RemoveAll(item => indexList.Contains(item.Index));

            this.RebuildIndexes();
        }

        internal void Shuffle()
        {
            int count = this.playlist.Count;

            var random = new Random();

            for (int index = 0; index < count; index++)
            {
                int newIndex = random.Next(count);

                // Migrate the CurrentSongIndex to the new position
                if (index == this.CurrentSongIndex)
                {
                    this.CurrentSongIndex = newIndex;
                }

                else if (newIndex == this.CurrentSongIndex)
                {
                    this.CurrentSongIndex = index;
                }

                PlaylistEntry temp = this.playlist[index];

                this.playlist[newIndex].Index = index;
                this.playlist[index] = this.playlist[newIndex];

                temp.Index = newIndex;
                this.playlist[newIndex] = temp;
            }
        }

        private void RebuildIndexes()
        {
            int index = 0;
            int? migrateIndex = null;
            var newPlaylist = new List<PlaylistEntry>(this.playlist.Capacity);

            foreach (var entry in this.playlist)
            {
                if (this.CurrentSongIndex == entry.Index)
                {
                    migrateIndex = index;
                }

                newPlaylist.Add(entry);
                entry.Index = index;

                index++;
            }

            this.playlist.Clear();
            this.playlist.AddRange(newPlaylist);

            if (migrateIndex.HasValue)
            {
                this.CurrentSongIndex = migrateIndex;
            }
        }
    }
}