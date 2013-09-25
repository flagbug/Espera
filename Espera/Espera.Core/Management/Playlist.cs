﻿using Rareform.Validation;
using ReactiveMarrow;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;

namespace Espera.Core.Management
{
    /// <summary>
    /// Represents a playlist where songs are stored with an associated index.
    /// </summary>
    public sealed class Playlist : IEnumerable<PlaylistEntry>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private readonly ReactiveList<PlaylistEntry> playlist;
        private string name;

        internal Playlist(string name, bool isTemporary = false)
        {
            this.Name = name;
            this.IsTemporary = isTemporary;

            this.playlist = new ReactiveList<PlaylistEntry>();

            this.CurrentSongIndex = new ReactiveProperty<int?>(x => x == null || this.ContainsIndex(x.Value), typeof(ArgumentOutOfRangeException));

            var canPlayNextSong = this.CurrentSongIndex
                .CombineLatest(this.playlist.Changed, (i, args) => i.HasValue && this.ContainsIndex(i.Value + 1))
                .Publish(false);
            canPlayNextSong.Connect();
            this.CanPlayNextSong = canPlayNextSong;

            var canPlayPeviousSong = this.CurrentSongIndex
                .CombineLatest(this.playlist.Changed, (i, args) => i.HasValue && this.ContainsIndex(i.Value - 1))
                .Publish(false);
            canPlayPeviousSong.Connect();
            this.CanPlayPreviousSong = canPlayPeviousSong;
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
        public IObservable<bool> CanPlayNextSong { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the previous song in the playlist can be played.
        /// </summary>
        /// <value>
        /// true if the previous song in the playlist can be played; otherwise, false.
        /// </value>
        public IObservable<bool> CanPlayPreviousSong { get; private set; }

        /// <summary>
        /// Gets or sets the index of the currently played song in the playlist.
        /// </summary>
        /// <value>
        /// The index of the currently played song in the playlist. <c>null</c>, if no song is currently played.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">The value is not in the range of the playlist's indexes.</exception>
        public ReactiveProperty<int?> CurrentSongIndex { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this playlist is temporary and used for instant-playing.
        /// This means that this playlist isn't saved to the harddrive when closing the application.
        /// </summary>
        public bool IsTemporary { get; private set; }

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

                int maxIndex = this.playlist.Count - 1;

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

            int index = this.playlist.Count;

            var itemsToAdd = songList.Select(song => new PlaylistEntry(index++, song)).ToList();

            this.playlist.AddRange(itemsToAdd);
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

            if (this.CurrentSongIndex.Value.HasValue && indexList.Contains(this.CurrentSongIndex.Value.Value))
            {
                this.CurrentSongIndex.Value = null;
            }

            this.playlist.RemoveAll(item => indexList.Contains(item.Index));

            this.RebuildIndexes();
        }

        internal void Shuffle()
        {
            this.playlist.Shuffle();

            this.RebuildIndexes();
        }

        private void RebuildIndexes()
        {
            int index = 0;
            int? migrateIndex = null;

            foreach (var entry in this.playlist)
            {
                if (this.CurrentSongIndex.Value == entry.Index)
                {
                    migrateIndex = index;
                }

                entry.Index = index;

                index++;
            }

            if (migrateIndex.HasValue)
            {
                this.CurrentSongIndex.Value = migrateIndex;
            }
        }
    }
}