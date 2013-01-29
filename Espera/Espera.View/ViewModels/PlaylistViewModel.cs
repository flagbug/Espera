using Espera.Core.Helpers;
using Espera.Core.Management;
using Rareform.Extensions;
using Rareform.Reflection;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Reactive.Disposables;

namespace Espera.View.ViewModels
{
    internal sealed class PlaylistViewModel : ReactiveObject, IDataErrorInfo, IDisposable
    {
        private readonly CompositeDisposable disposable;
        private readonly ReactiveCollection<PlaylistEntryViewModel> entries;
        private readonly Playlist playlist;
        private readonly Func<string, bool> renameRequest;
        private readonly ObservableAsPropertyHelper<int> songCount;
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

            this.entries = playlist.CreateDerivedCollection(entry => new PlaylistEntryViewModel(entry));
            this.entries.ItemsRemoved.Subscribe(x => x.Dispose());

            this.songCount = this.entries.CollectionCountChanged.ToProperty(this, x => x.SongCount, this.entries.Count);

            this.playlist.CurrentSongIndexChanged.Subscribe(this.UpdateCurrentSong).DisposeWith(this.disposable);
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

                    this.RaisePropertyChanged(x => x.EditName);
                }
            }
        }

        public string Error
        {
            get { return null; }
        }

        public string Name
        {
            get { return this.playlist.Name; }
            set
            {
                if (this.Name != value)
                {
                    this.playlist.Name = value;
                    this.RaisePropertyChanged(x => x.Name);
                }
            }
        }

        public int SongCount
        {
            get { return this.songCount.Value; }
        }

        public ReactiveCollection<PlaylistEntryViewModel> Songs
        {
            get { return this.entries; }
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
            if (currentSongIndex == null)
            {
                foreach (PlaylistEntryViewModel entry in entries)
                {
                    entry.IsPlaying = false;
                }
            }

            else
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