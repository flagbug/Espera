using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.Core.Management
{
    /// <summary>
    /// Represents a playlist where songs are stored with an associated index.
    /// </summary>
    public sealed class Playlist : ReactiveObject, IEnumerable<PlaylistEntry>, INotifyCollectionChanged
    {
        private readonly ObservableAsPropertyHelper<bool> canPlayNextSong;
        private readonly ObservableAsPropertyHelper<bool> canPlayPreviousSong;
        private readonly ReactiveList<PlaylistEntry> playlist;
        private int? currentSongIndex;
        private string name;

        internal Playlist(string name, bool isTemporary = false)
        {
            this.Name = name;
            this.IsTemporary = isTemporary;

            this.playlist = new ReactiveList<PlaylistEntry>();

            this.WhenAnyValue(x => x.CurrentSongIndex).Where(x => x != null).Subscribe(x =>
            {
                this.ResetVotesBeforeIndex(x.Value);
            });

            this.canPlayNextSong = this.WhenAnyValue(x => x.CurrentSongIndex)
                .CombineLatest(this.playlist.Changed.ToUnit().StartWith(Unit.Default), (i, _) => i.HasValue && this.ContainsIndex(i.Value + 1))
                .ToProperty(this, x => x.CanPlayNextSong);

            this.canPlayPreviousSong = this.WhenAnyValue(x => x.CurrentSongIndex)
                .CombineLatest(this.playlist.Changed.ToUnit().StartWith(Unit.Default), (i, _) => i.HasValue && this.ContainsIndex(i.Value - 1))
                .ToProperty(this, x => x.CanPlayPreviousSong);
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Gets a value indicating whether the next song in the playlist can be played.
        /// </summary>
        /// <value>true if the next song in the playlist can be played; otherwise, false.</value>
        public bool CanPlayNextSong
        {
            get { return this.canPlayNextSong.Value; }
        }

        /// <summary>
        /// Gets a value indicating whether the previous song in the playlist can be played.
        /// </summary>
        /// <value>true if the previous song in the playlist can be played; otherwise, false.</value>
        public bool CanPlayPreviousSong
        {
            get { return this.canPlayPreviousSong.Value; }
        }

        /// <summary>
        /// Gets or sets the index of the currently played song in the playlist.
        /// </summary>
        /// <value>
        /// The index of the currently played song in the playlist. <c>null</c> , if no song is
        /// currently played.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The value is not in the range of the playlist's indexes.
        /// </exception>
        public int? CurrentSongIndex
        {
            get { return this.currentSongIndex; }
            set
            {
                if (value != null && !this.ContainsIndex(value.Value))
                    throw new ArgumentOutOfRangeException("value");

                this.RaiseAndSetIfChanged(ref this.currentSongIndex, value);
            }
        }

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

        internal PlaylistEntry AddShadowVotedSong(Song song)
        {
            this.AddSongs(new[] { song });

            PlaylistEntry entry = this.Last();
            entry.ShadowVote();

            return entry;
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

            // We don't use the change notification directly, but record them for later. This allows
            // us to have the correct semantics, even if we rebuild the indexes after the changes to
            // the list were made
            using (this.WithIndexRebuild())
            {
                this.playlist.AddRange(itemsToAdd);
            }
        }

        internal void MoveSong(int fromIndex, int toIndex)
        {
            if (fromIndex >= this.playlist.Count)
                Throw.ArgumentOutOfRangeException(() => fromIndex);

            if (fromIndex < 0)
                Throw.ArgumentOutOfRangeException(() => fromIndex);

            if (toIndex >= this.playlist.Count)
                Throw.ArgumentOutOfRangeException(() => fromIndex);

            if (toIndex < 0)
                Throw.ArgumentOutOfRangeException(() => fromIndex);

            using (this.WithIndexRebuild())
            {
                this.playlist.Move(fromIndex, toIndex);
            }
        }

        /// <summary>
        /// Removes the songs with the specified indexes from the <see cref="Playlist" /> .
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

            var itemsToRemove = this.playlist.Where(x => indexList.Contains(x.Index)).ToList();

            using (this.WithIndexRebuild())
            {
                this.playlist.RemoveAll(itemsToRemove);
            }
        }

        internal void Shuffle()
        {
            var newList = new List<PlaylistEntry>(this.playlist.Count);
            newList.AddRange(this.playlist.OrderBy(x => Guid.NewGuid()));

            this.playlist.Clear();
            this.playlist.AddRange(newList);

            this.RebuildIndexes();

            this.OnCollectionChanged(Observable.Return(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
        }

        internal PlaylistEntry VoteFor(int index)
        {
            if (index < 0)
                Throw.ArgumentOutOfRangeException(() => index, 0);

            if (index > this.playlist.Count)
                Throw.ArgumentOutOfRangeException(() => index, this.playlist.Count);

            if (this.CurrentSongIndex.HasValue && index <= this.CurrentSongIndex.Value)
                throw new InvalidOperationException("Index can't be less or equal the current song index");

            PlaylistEntry entry = this[index];
            entry.Vote();

            if (this.playlist.Count == 1 || (this.CurrentSongIndex.HasValue && index == this.CurrentSongIndex.Value + 1))
                return entry;

            var targetEntry = this.Skip(this.CurrentSongIndex.HasValue ?
                    this.CurrentSongIndex.Value + 1 : 0)
                .SkipWhile(x => x.Votes >= this[index].Votes && x != this[index])
                .First();

            using (this.WithIndexRebuild())
            {
                this.playlist.Move(index, targetEntry.Index);
            }

            return entry;
        }

        private void OnCollectionChanged(IObservable<NotifyCollectionChangedEventArgs> args)
        {
            if (this.CollectionChanged == null)
                return;

            args.Subscribe(x => this.CollectionChanged(this, x));
        }

        private void RebuildIndexes()
        {
            int index = 0;
            int? migrateIndex = null;

            foreach (var entry in this.playlist)
            {
                if (this.CurrentSongIndex == entry.Index)
                {
                    migrateIndex = index;
                }

                entry.Index = index;

                index++;
            }

            if (migrateIndex.HasValue)
            {
                this.CurrentSongIndex = migrateIndex;
            }
        }

        private void ResetVotesBeforeIndex(int index)
        {
            for (int i = 0; i < index; i++)
            {
                this[i].ResetVotes();
            }
        }

        /// <summary>
        /// Records the collection changes that are made unitil the method is disposed, rebuilds the
        /// playlist indexes and then sends the change notification to the subscribers.
        /// </summary>
        private IDisposable WithIndexRebuild()
        {
            var recordedChanges = this.playlist.Changed.Replay();
            IDisposable record = recordedChanges.Connect();

            var rebuildAndNotify = Disposable.Create(() =>
            {
                this.RebuildIndexes();

                record.Dispose();
                this.OnCollectionChanged(recordedChanges);
            });

            return rebuildAndNotify;
        }
    }
}