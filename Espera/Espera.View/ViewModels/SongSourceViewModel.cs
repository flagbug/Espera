using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Espera.Core.Management;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public abstract class SongSourceViewModel<T> : ReactiveObject, ISongSourceViewModel
        where T : ISongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<bool> isAdmin;
        private readonly Library library;
        private readonly Subject<Unit> timeoutWarning;
        private string searchText;
        private IEnumerable<T> selectableSongs;
        private IEnumerable<ISongViewModelBase> selectedSongs;

        protected SongSourceViewModel(Library library, Guid accessToken)
        {
            this.library = library;

            this.searchText = String.Empty;
            this.selectableSongs = Enumerable.Empty<T>();
            this.timeoutWarning = new Subject<Unit>();

            this.AddToPlaylistCommand = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.SelectedSongs, x => x != null && x.Any()));
            this.AddToPlaylistCommand.Subscribe(x =>
            {
                if (this.library.RemainingPlaylistTimeout > TimeSpan.Zero)
                {
                    // Trigger the animation
                    this.timeoutWarning.OnNext(Unit.Default);

                    return;
                }

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
                    this.library.AddSongToPlaylist(this.SelectedSongs.Select(song => song.Model).Single());
                }
            });

            this.SelectionChangedCommand = new ReactiveUI.Legacy.ReactiveCommand();
            this.SelectionChangedCommand.Where(x => x != null)
                .Select(x => ((IEnumerable)x).Cast<ISongViewModelBase>())
                .Subscribe(x => this.SelectedSongs = x);

            this.isAdmin = this.Library.LocalAccessControl.ObserveAccessPermission(accessToken)
                .Select(x => x == AccessPermission.Admin)
                .ToProperty(this, x => x.IsAdmin);
        }

        public ReactiveUI.Legacy.ReactiveCommand AddToPlaylistCommand { get; private set; }

        public bool IsAdmin
        {
            get { return this.isAdmin.Value; }
        }

        public abstract ReactiveUI.Legacy.ReactiveCommand PlayNowCommand { get; }

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

        public ReactiveUI.Legacy.ReactiveCommand SelectionChangedCommand { get; set; }

        public IObservable<Unit> TimeoutWarning
        {
            get { return this.timeoutWarning.AsObservable(); }
        }

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