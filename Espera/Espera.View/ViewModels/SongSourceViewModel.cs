using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Espera.Core.Management;
using Espera.Core.Settings;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public abstract class SongSourceViewModel<T> : ReactiveObject, ISongSourceViewModel
        where T : ISongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<bool> isAdmin;
        private readonly Library library;
        private SortOrder durationOrder;
        private string searchText;
        private IEnumerable<T> selectableSongs;
        private IEnumerable<ISongViewModelBase> selectedSongs;
        private SortOrder titleOrder;

        protected SongSourceViewModel(Library library, Guid accessToken)
        {
            this.library = library;

            this.searchText = String.Empty;
            this.selectableSongs = Enumerable.Empty<T>();

            this.ApplyOrder(SortHelpers.GetOrderByTitle<T>, ref this.titleOrder);

            this.AddToPlaylistCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.SelectedSongs, x => x != null && x.Any()));
            this.AddToPlaylistCommand.Subscribe(x =>
            {
                if (this.IsAdmin)
                {
                    this.library.AddSongsToPlaylist(this.SelectedSongs.Select(song => song.Model), accessToken);

                    if (x != null)
                    {
                        this.library.MovePlaylistSong(this.library.CurrentPlaylist.Last().Index, (int)x, accessToken);
                    }
                }

                else
                {
                    this.library.AddGuestSongToPlaylist(this.SelectedSongs.Select(song => song.Model).Single(), accessToken);
                }
            });

            this.SelectionChangedCommand = ReactiveCommand.Create();
            this.SelectionChangedCommand.Where(x => x != null)
                .Select(x => ((IEnumerable)x).Cast<ISongViewModelBase>())
                .Subscribe(x => this.SelectedSongs = x);

            this.isAdmin = this.Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin)
                .ToProperty(this, x => x.IsAdmin);

            this.OrderByDurationCommand = ReactiveCommand.Create();
            this.OrderByDurationCommand.Subscribe(_ => this.ApplyOrder(SortHelpers.GetOrderByDuration<T>, ref this.durationOrder));

            this.OrderByTitleCommand = ReactiveCommand.Create();
            this.OrderByTitleCommand.Subscribe(_ => this.ApplyOrder(SortHelpers.GetOrderByTitle<T>, ref this.titleOrder));
        }

        public ReactiveCommand<object> AddToPlaylistCommand { get; private set; }

        public abstract DefaultPlaybackAction DefaultPlaybackAction { get; }

        public bool IsAdmin
        {
            get { return this.isAdmin.Value; }
        }

        public ReactiveCommand<object> OrderByDurationCommand { get; private set; }

        public ReactiveCommand<object> OrderByTitleCommand { get; private set; }

        public abstract ReactiveCommand<Unit> PlayNowCommand { get; }

        public string SearchText
        {
            get { return this.searchText; }
            set { this.RaiseAndSetIfChanged(ref this.searchText, value); }
        }

        public IEnumerable<T> SelectableSongs
        {
            get { return this.selectableSongs; }
            protected set { this.RaiseAndSetIfChanged(ref this.selectableSongs, value); }
        }

        public IEnumerable<ISongViewModelBase> SelectedSongs
        {
            get { return this.selectedSongs; }
            set { this.RaiseAndSetIfChanged(ref this.selectedSongs, value); }
        }

        public ReactiveCommand<object> SelectionChangedCommand { get; set; }

        protected Library Library
        {
            get { return this.library; }
        }

        protected Func<IEnumerable<T>, IOrderedEnumerable<T>> SongOrderFunc { get; private set; }

        protected void ApplyOrder(Func<SortOrder, Func<IEnumerable<T>, IOrderedEnumerable<T>>> orderFunc, ref SortOrder sortOrder)
        {
            this.SongOrderFunc = orderFunc(sortOrder);
            SortHelpers.InverseOrder(ref sortOrder);

            this.SelectableSongs = this.SongOrderFunc(this.SelectableSongs);
        }
    }
}