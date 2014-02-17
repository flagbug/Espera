using Espera.Core.Management;
using Rareform.Extensions;
using Rareform.Reflection;
using ReactiveMarrow;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    internal sealed class PlaylistViewModel : ReactiveObject, IDataErrorInfo, IDisposable
    {
        private readonly CompositeDisposable disposable;
        private readonly IReactiveDerivedList<PlaylistEntryViewModel> entries;
        private readonly Playlist playlist;
        private readonly Func<string, bool> renameRequest;
        private readonly ObservableAsPropertyHelper<int> songsRemaining;
        private readonly ObservableAsPropertyHelper<TimeSpan?> timeRemaining;
        private bool editName;
        private string saveName;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistViewModel"/> class.
        /// </summary>
        /// <param name="playlist">The playlist info.</param>
        /// <param name="renameRequest">A function that requests the rename of the playlist. Return true, if the rename is granted, otherwise false.</param>
        public PlaylistViewModel(Playlist playlist, Func<string, bool> renameRequest)
        {
            this.playlist = playlist;
            this.renameRequest = renameRequest;

            this.disposable = new CompositeDisposable();

            this.entries = playlist
                .CreateDerivedCollection(entry => new PlaylistEntryViewModel(entry))
                .DisposeWith(this.disposable);
            this.entries.ItemsRemoved.Subscribe(x => x.Dispose()).DisposeWith(this.disposable);

            IObservable<int?> currentSongUpdated = this.playlist.CurrentSongIndex.Do(this.UpdateCurrentSong);
            IObservable<IEnumerable<PlaylistEntryViewModel>> remainingSongs = this.entries.Changed
                .Select(x => Unit.Default)
                .Merge(currentSongUpdated.Select(x => Unit.Default))
                .Select(x => this.entries.Reverse().TakeWhile(entry => !entry.IsPlaying).ToList());

            this.songsRemaining = remainingSongs
                .Select(x => x.Count())
                .ToProperty(this, x => x.SongsRemaining)
                .DisposeWith(this.disposable);

            this.timeRemaining = remainingSongs
                .Select(x => x.Any() ? x.Select(entry => entry.Duration).Aggregate((t1, t2) => t1 + t2) : (TimeSpan?)null)
                .ToProperty(this, x => x.TimeRemaining)
                .DisposeWith(this.disposable);
        }

        public bool EditName
        {
            get { return this.editName; }
            set
            {
                if (this.EditName != value)
                {
                    this.editName = value;

                    if (this.EditName)
                    {
                        this.saveName = this.Name;
                    }

                    else if (this.HasErrors())
                    {
                        this.Name = this.saveName;
                        this.saveName = null;
                    }

                    this.RaisePropertyChanged();
                }
            }
        }

        public string Error
        {
            get { return null; }
        }

        public Playlist Model
        {
            get { return this.playlist; }
        }

        public string Name
        {
            get { return this.playlist.IsTemporary ? "Now Playing" : this.playlist.Name; }
            set
            {
                if (this.Name != value)
                {
                    this.playlist.Name = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public IReadOnlyReactiveCollection<PlaylistEntryViewModel> Songs
        {
            get { return this.entries; }
        }

        /// <summary>
        /// Gets the number of songs that come after the currently played song.
        /// </summary>
        public int SongsRemaining
        {
            get { return this.songsRemaining.Value; }
        }

        /// <summary>
        /// Gets the total remaining time of all songs that come after the currently played song.
        /// </summary>
        public TimeSpan? TimeRemaining
        {
            get { return this.timeRemaining.Value; }
        }

        public string this[string columnName]
        {
            get
            {
                string error = null;

                if (columnName == Reflector.GetMemberName(() => this.Name))
                {
                    if (!this.renameRequest(this.Name))
                    {
                        error = "Name already exists.";
                    }

                    else if (String.IsNullOrWhiteSpace(this.Name))
                    {
                        error = "Name cannot be empty or whitespace.";
                    }
                }

                return error;
            }
        }

        public void Dispose()
        {
            this.disposable.Dispose();

            foreach (PlaylistEntryViewModel entry in entries)
            {
                entry.Dispose();
            }
        }

        private void UpdateCurrentSong(int? currentSongIndex)
        {
            foreach (PlaylistEntryViewModel entry in entries)
            {
                entry.IsPlaying = false;
            }

            if (currentSongIndex.HasValue)
            {
                PlaylistEntryViewModel entry = this.entries[currentSongIndex.Value];

                if (!entry.IsCorrupted)
                {
                    entry.IsPlaying = true;
                }
            }
        }
    }
}