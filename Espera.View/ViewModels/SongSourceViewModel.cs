using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Espera.Core.Management;
using Espera.Core.Settings;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public abstract class SongSourceViewModel<T> : ReactiveObject, ISongSourceViewModel
        where T : ISongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<IReactiveCommand> defaultPlaybackCommand;
        private readonly ObservableAsPropertyHelper<bool> isAdmin;
        private SortOrder durationOrder;
        private string searchText;
        private IEnumerable<T> selectableSongs;
        private IEnumerable<ISongViewModelBase> selectedSongs;
        private SortOrder titleOrder;

        protected SongSourceViewModel(Library library, CoreSettings coreSettings, Guid accessToken)
        {
            if (library == null)
                throw new ArgumentNullException("library");

            if (coreSettings == null)
                throw new ArgumentNullException("coreSettings");

            Library = library;
            CoreSettings = coreSettings;

            searchText = string.Empty;
            selectableSongs = Enumerable.Empty<T>();

            ApplyOrder(SortHelpers.GetOrderByTitle<T>, ref titleOrder);

            var canAddToPlaylist = this.WhenAnyValue(x => x.SelectedSongs, x => x.Any())
                .CombineLatest(
                    Library.LocalAccessControl.HasAccess(CoreSettings.WhenAnyValue(x => x.LockPlaylist), accessToken),
                    (songsSelected, hasAccess) => songsSelected && hasAccess);

            AddToPlaylistCommand = ReactiveCommand.Create(canAddToPlaylist);
            AddToPlaylistCommand.Subscribe(x =>
            {
                if (IsAdmin)
                {
                    Library.AddSongsToPlaylist(SelectedSongs.Select(song => song.Model), accessToken);

                    if (x != null)
                        Library.MovePlaylistSong(Library.CurrentPlaylist.Last().Index, (int)x, accessToken);
                }

                else
                {
                    Library.AddGuestSongToPlaylist(SelectedSongs.Select(song => song.Model).Single(), accessToken);
                }
            });

            SelectionChangedCommand = ReactiveCommand.Create();
            SelectionChangedCommand.Where(x => x != null)
                .Select(x => ((IEnumerable)x).Cast<ISongViewModelBase>())
                .Subscribe(x => SelectedSongs = x);

            this.isAdmin = Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin)
                .ToProperty(this, x => x.IsAdmin);

            // The default play command differs whether we are in party mode or not and depends on
            // the selected setting in administrator mode and the song source.
            //
            // In party mode, it is always "Add To Playlist", in administrator mode we look at the
            // value that the song source returns
            defaultPlaybackCommand = this.WhenAnyValue(x => x.IsAdmin,
                    isAdmin => !isAdmin || DefaultPlaybackAction == DefaultPlaybackAction.AddToPlaylist
                        ? (IReactiveCommand)AddToPlaylistCommand
                        : PlayNowCommand)
                .ToProperty(this, x => x.DefaultPlaybackCommand);

            OrderByDurationCommand = ReactiveCommand.Create();
            OrderByDurationCommand.Subscribe(_ => ApplyOrder(SortHelpers.GetOrderByDuration<T>, ref durationOrder));

            OrderByTitleCommand = ReactiveCommand.Create();
            OrderByTitleCommand.Subscribe(_ => ApplyOrder(SortHelpers.GetOrderByTitle<T>, ref titleOrder));
        }

        public IReactiveCommand DefaultPlaybackCommand => defaultPlaybackCommand.Value;

        public bool IsAdmin => isAdmin.Value;

        public ReactiveCommand<object> OrderByDurationCommand { get; }

        public ReactiveCommand<object> OrderByTitleCommand { get; }

        public IEnumerable<T> SelectableSongs
        {
            get => selectableSongs ?? Enumerable.Empty<T>();
            protected set => this.RaiseAndSetIfChanged(ref selectableSongs, value);
        }

        public ReactiveCommand<object> SelectionChangedCommand { get; set; }

        protected CoreSettings CoreSettings { get; }

        protected Library Library { get; }

        protected Func<IEnumerable<T>, IOrderedEnumerable<T>> SongOrderFunc { get; private set; }

        public ReactiveCommand<object> AddToPlaylistCommand { get; }

        public abstract DefaultPlaybackAction DefaultPlaybackAction { get; }

        public abstract ReactiveCommand<Unit> PlayNowCommand { get; }

        public string SearchText
        {
            get => searchText;
            set => this.RaiseAndSetIfChanged(ref searchText, value);
        }

        public IEnumerable<ISongViewModelBase> SelectedSongs
        {
            get => selectedSongs ?? Enumerable.Empty<ISongViewModelBase>();
            set => this.RaiseAndSetIfChanged(ref selectedSongs, value);
        }

        protected void ApplyOrder(Func<SortOrder, Func<IEnumerable<T>, IOrderedEnumerable<T>>> orderFunc,
            ref SortOrder sortOrder)
        {
            SongOrderFunc = orderFunc(sortOrder);
            SortHelpers.InverseOrder(ref sortOrder);

            SelectableSongs = SongOrderFunc(SelectableSongs);
        }
    }
}