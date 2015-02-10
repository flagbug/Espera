using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Espera.Core.Management;
using Espera.Core.Settings;
using Rareform.Extensions;
using Rareform.Reflection;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public class PlaylistViewModel : ReactiveObject, IDataErrorInfo, IDisposable
    {
        private readonly ObservableAsPropertyHelper<bool> canAlterPlaylist;
        private readonly CompositeDisposable disposable;
        private readonly IReactiveDerivedList<PlaylistEntryViewModel> entries;
        private readonly Library library;
        private readonly Playlist playlist;
        private readonly ObservableAsPropertyHelper<int> songsRemaining;
        private readonly ObservableAsPropertyHelper<TimeSpan?> timeRemaining;
        private bool editName;
        private string saveName;

        private IEnumerable<PlaylistEntryViewModel> selectedEntries;

        public PlaylistViewModel(Playlist playlist, Library library, Guid accessToken, CoreSettings coreSettings)
        {
            if (playlist == null)
                throw new ArgumentNullException("playlist");

            if (library == null)
                throw new ArgumentNullException("library");

            if (coreSettings == null)
                throw new ArgumentNullException("coreSettings");

            this.playlist = playlist;
            this.library = library;

            this.disposable = new CompositeDisposable();

            this.entries = playlist
                .CreateDerivedCollection(entry => new PlaylistEntryViewModel(entry), x => x.Dispose())
                .DisposeWith(this.disposable);

            this.playlist.WhenAnyValue(x => x.CurrentSongIndex).ToUnit()
                .Merge(this.entries.Changed.ToUnit())
                .Subscribe(_ => this.UpdateCurrentSong())
                .DisposeWith(this.disposable);

            IObservable<List<PlaylistEntryViewModel>> remainingSongs = this.entries.Changed
                .Select(x => Unit.Default)
                .Merge(this.playlist.WhenAnyValue(x => x.CurrentSongIndex).ToUnit())
                .Select(x => this.entries.Reverse().TakeWhile(entry => !entry.IsPlaying).ToList());

            this.songsRemaining = remainingSongs
                .Select(x => x.Count)
                .ToProperty(this, x => x.SongsRemaining)
                .DisposeWith(this.disposable);

            this.timeRemaining = remainingSongs
                .Select(x => x.Any() ? x.Select(entry => entry.Duration).Aggregate((t1, t2) => t1 + t2) : (TimeSpan?)null)
                .ToProperty(this, x => x.TimeRemaining)
                .DisposeWith(this.disposable);

            this.CurrentPlayingEntry = this.Model.WhenAnyValue(x => x.CurrentSongIndex).Select(x => x == null ? null : this.entries[x.Value]);

            this.canAlterPlaylist = this.library.LocalAccessControl.HasAccess(coreSettings.WhenAnyValue(x => x.LockPlaylist), accessToken)
                .ToProperty(this, x => x.CanAlterPlaylist)
                .DisposeWith(disposable);

            // We re-evaluate the selected entries after each up or down move here, because WPF
            // doesn't send us proper updates about the selection
            var reEvaluateSelectedPlaylistEntry = new Subject<Unit>();
            this.MovePlaylistSongUpCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => this.SelectedEntries))
                .Select(x => x != null && x.Count() == 1 && x.First().Index > 0)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist), (canMoveUp, canAlterPlaylist) => canMoveUp && canAlterPlaylist));
            this.MovePlaylistSongUpCommand.Subscribe(_ =>
            {
                int index = this.SelectedEntries.First().Index;
                this.library.MovePlaylistSong(index, index - 1, accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            this.MovePlaylistSongDownCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => this.SelectedEntries))
                .Select(x => x != null && x.Count() == 1 && x.First().Index < this.Songs.Count - 1)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist), (canMoveDown, canAlterPlaylist) => canMoveDown && canAlterPlaylist));
            this.MovePlaylistSongDownCommand.Subscribe(_ =>
            {
                int index = this.SelectedEntries.First().Index;
                this.library.MovePlaylistSong(index, index + 1, accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            this.MovePlaylistSongCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => this.SelectedEntries))
                .Select(x => x != null && x.Count() == 1)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist), (canMoveUp, canAlterPlaylist) => canMoveUp && canAlterPlaylist));
            this.MovePlaylistSongCommand.Subscribe(x =>
            {
                int fromIndex = this.SelectedEntries.First().Index;
                int toIndex = (int?)x ?? this.Songs.Last().Index + 1;

                // If we move a song from the front of the playlist to the back, we want it move be
                // in front of the target song
                if (fromIndex < toIndex)
                {
                    toIndex--;
                }

                this.library.MovePlaylistSong(fromIndex, toIndex, accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            this.RemoveSelectedPlaylistEntriesCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedEntries, x => x.CanAlterPlaylist,
                (selectedPlaylistEntries, canAlterPlaylist) => selectedPlaylistEntries != null && selectedPlaylistEntries.Any() && canAlterPlaylist));
            this.RemoveSelectedPlaylistEntriesCommand.Subscribe(x => this.library.RemoveFromPlaylist(this.SelectedEntries.Select(entry => entry.Index), accessToken));
        }

        public bool CanAlterPlaylist
        {
            get { return this.canAlterPlaylist.Value; }
        }

        public IObservable<PlaylistEntryViewModel> CurrentPlayingEntry { get; private set; }

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

        public ReactiveCommand<object> MovePlaylistSongCommand { get; private set; }

        public ReactiveCommand<object> MovePlaylistSongDownCommand { get; private set; }

        public ReactiveCommand<object> MovePlaylistSongUpCommand { get; private set; }

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

        public ReactiveCommand<object> RemoveSelectedPlaylistEntriesCommand { get; private set; }

        public IEnumerable<PlaylistEntryViewModel> SelectedEntries
        {
            get { return this.selectedEntries ?? Enumerable.Empty<PlaylistEntryViewModel>(); }
            set { this.RaiseAndSetIfChanged(ref this.selectedEntries, value); }
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
                    if (this.library.Playlists.Count(p => p.Name == this.Name) > 1)
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

        private void UpdateCurrentSong()
        {
            foreach (PlaylistEntryViewModel entry in entries)
            {
                entry.IsPlaying = false;
            }

            if (this.playlist.CurrentSongIndex.HasValue)
            {
                PlaylistEntryViewModel entry = this.entries[this.playlist.CurrentSongIndex.Value];

                if (!entry.IsCorrupted)
                {
                    entry.IsPlaying = true;
                }
            }
        }
    }
}