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

            Model = playlist;
            this.library = library;

            disposable = new CompositeDisposable();

            entries = playlist
                .CreateDerivedCollection(entry => new PlaylistEntryViewModel(entry), x => x.Dispose())
                .DisposeWith(disposable);

            Model.WhenAnyValue(x => x.CurrentSongIndex).ToUnit()
                .Merge(entries.Changed.ToUnit())
                .Subscribe(_ => UpdateCurrentSong())
                .DisposeWith(disposable);

            var remainingSongs = entries.Changed
                .Select(x => Unit.Default)
                .Merge(Model.WhenAnyValue(x => x.CurrentSongIndex).ToUnit())
                .Select(x => entries.Reverse().TakeWhile(entry => !entry.IsPlaying).ToList());

            songsRemaining = remainingSongs
                .Select(x => x.Count)
                .ToProperty(this, x => x.SongsRemaining)
                .DisposeWith(disposable);

            timeRemaining = remainingSongs
                .Select(x =>
                    x.Any() ? x.Select(entry => entry.Duration).Aggregate((t1, t2) => t1 + t2) : (TimeSpan?)null)
                .ToProperty(this, x => x.TimeRemaining)
                .DisposeWith(disposable);

            CurrentPlayingEntry = Model.WhenAnyValue(x => x.CurrentSongIndex)
                .Select(x => x == null ? null : entries[x.Value]);

            this.canAlterPlaylist = this.library.LocalAccessControl
                .HasAccess(coreSettings.WhenAnyValue(x => x.LockPlaylist), accessToken)
                .ToProperty(this, x => x.CanAlterPlaylist)
                .DisposeWith(disposable);

            // We re-evaluate the selected entries after each up or down move here, because WPF
            // doesn't send us proper updates about the selection
            var reEvaluateSelectedPlaylistEntry = new Subject<Unit>();
            MovePlaylistSongUpCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => SelectedEntries))
                .Select(x => x != null && x.Count() == 1 && x.First().Index > 0)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist),
                    (canMoveUp, canAlterPlaylist) => canMoveUp && canAlterPlaylist));
            MovePlaylistSongUpCommand.Subscribe(_ =>
            {
                var index = SelectedEntries.First().Index;
                this.library.MovePlaylistSong(index, index - 1, accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            MovePlaylistSongDownCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => SelectedEntries))
                .Select(x => x != null && x.Count() == 1 && x.First().Index < Songs.Count - 1)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist),
                    (canMoveDown, canAlterPlaylist) => canMoveDown && canAlterPlaylist));
            MovePlaylistSongDownCommand.Subscribe(_ =>
            {
                var index = SelectedEntries.First().Index;
                this.library.MovePlaylistSong(index, index + 1, accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            MovePlaylistSongCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedEntries)
                .Merge(reEvaluateSelectedPlaylistEntry.Select(_ => SelectedEntries))
                .Select(x => x != null && x.Count() == 1)
                .CombineLatest(this.WhenAnyValue(x => x.CanAlterPlaylist),
                    (canMoveUp, canAlterPlaylist) => canMoveUp && canAlterPlaylist));
            MovePlaylistSongCommand.Subscribe(x =>
            {
                var fromIndex = SelectedEntries.First().Index;
                var toIndex = (int?)x ?? Songs.Last().Index + 1;

                // If we move a song from the front of the playlist to the back, we want it move be
                // in front of the target song
                if (fromIndex < toIndex) toIndex--;

                this.library.MovePlaylistSong(fromIndex, toIndex, accessToken);
                reEvaluateSelectedPlaylistEntry.OnNext(Unit.Default);
            });

            RemoveSelectedPlaylistEntriesCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedEntries,
                x => x.CanAlterPlaylist,
                (selectedPlaylistEntries, canAlterPlaylist) => selectedPlaylistEntries != null &&
                                                               selectedPlaylistEntries.Any() && canAlterPlaylist));
            RemoveSelectedPlaylistEntriesCommand.Subscribe(x =>
                this.library.RemoveFromPlaylist(SelectedEntries.Select(entry => entry.Index), accessToken));
        }

        public bool CanAlterPlaylist => canAlterPlaylist.Value;

        public IObservable<PlaylistEntryViewModel> CurrentPlayingEntry { get; }

        public bool EditName
        {
            get => editName;
            set
            {
                if (EditName != value)
                {
                    editName = value;

                    if (EditName)
                    {
                        saveName = Name;
                    }

                    else if (this.HasErrors())
                    {
                        Name = saveName;
                        saveName = null;
                    }

                    this.RaisePropertyChanged();
                }
            }
        }

        public Playlist Model { get; }

        public ReactiveCommand<object> MovePlaylistSongCommand { get; }

        public ReactiveCommand<object> MovePlaylistSongDownCommand { get; }

        public ReactiveCommand<object> MovePlaylistSongUpCommand { get; }

        public string Name
        {
            get => Model.IsTemporary ? "Now Playing" : Model.Name;
            set
            {
                if (Name != value)
                {
                    Model.Name = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public ReactiveCommand<object> RemoveSelectedPlaylistEntriesCommand { get; }

        public IEnumerable<PlaylistEntryViewModel> SelectedEntries
        {
            get => selectedEntries ?? Enumerable.Empty<PlaylistEntryViewModel>();
            set => this.RaiseAndSetIfChanged(ref selectedEntries, value);
        }

        public IReadOnlyReactiveCollection<PlaylistEntryViewModel> Songs => entries;

        /// <summary>
        ///     Gets the number of songs that come after the currently played song.
        /// </summary>
        public int SongsRemaining => songsRemaining.Value;

        /// <summary>
        ///     Gets the total remaining time of all songs that come after the currently played song.
        /// </summary>
        public TimeSpan? TimeRemaining => timeRemaining.Value;

        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                string error = null;

                if (columnName == Reflector.GetMemberName(() => Name))
                {
                    if (library.Playlists.Count(p => p.Name == Name) > 1)
                        error = "Name already exists.";

                    else if (string.IsNullOrWhiteSpace(Name)) error = "Name cannot be empty or whitespace.";
                }

                return error;
            }
        }

        public void Dispose()
        {
            disposable.Dispose();

            foreach (var entry in entries) entry.Dispose();
        }

        private void UpdateCurrentSong()
        {
            foreach (var entry in entries) entry.IsPlaying = false;

            if (Model.CurrentSongIndex.HasValue)
            {
                var entry = entries[Model.CurrentSongIndex.Value];

                if (!entry.IsCorrupted) entry.IsPlaying = true;
            }
        }
    }
}