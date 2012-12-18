using Espera.Core.Management;
using Rareform.Extensions;
using ReactiveUI;
using ReactiveUI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal abstract class SongSourceViewModel<T> : ReactiveObject, ISongSourceViewModel
        where T : SongViewModelBase
    {
        private readonly Library library;
        private ObservableAsPropertyHelper<bool> isAdmin;
        private string searchText;
        private IEnumerable<T> selectableSongs;
        private IEnumerable<SongViewModelBase> selectedSongs;

        protected SongSourceViewModel(Library library)
        {
            this.library = library;

            this.searchText = String.Empty;
            this.selectableSongs = Enumerable.Empty<T>();

            this.WhenAny(x => x.SelectedSongs, x => Unit.Default)
                .Subscribe(p => this.RaisePropertyChanged(x => x.IsSongSelected));

            IObservable<bool> canAddToPlaylist = this.WhenAny(x => x.SelectedSongs, x => x.Value != null && x.Value.Any());
            var addToPlaylistCommand = new ReactiveCommand(canAddToPlaylist);
            this.AddToPlaylistCommand = addToPlaylistCommand;
            addToPlaylistCommand.Subscribe(p =>
            {
                if (!this.Library.CanAddSongToPlaylist)
                {
                    // Trigger the animation
                    this.TimeoutWarning.RaiseSafe(this, EventArgs.Empty);

                    return;
                }

                if (this.IsAdmin)
                {
                    this.library.AddSongsToPlaylist(this.SelectedSongs.Select(song => song.Model));
                }

                else
                {
                    this.library.AddSongToPlaylist(this.SelectedSongs.Select(song => song.Model).Single());
                }
            });

            this.isAdmin = this.Library.AccessMode
                .Select(x => x == AccessMode.Administrator)
                .ToProperty(this, x => x.IsAdmin);
        }

        public event EventHandler TimeoutWarning;

        public ICommand AddToPlaylistCommand { get; private set; }

        public bool IsAdmin
        {
            get { return this.isAdmin.Value; }
        }

        public bool IsSongSelected
        {
            get { return this.SelectedSongs != null && this.SelectedSongs.Any(); }
        }

        public string SearchText
        {
            get { return this.searchText; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public IEnumerable<T> SelectableSongs
        {
            get { return this.selectableSongs; }
            protected set { this.RaiseAndSetIfChanged(value); }
        }

        public IEnumerable<SongViewModelBase> SelectedSongs
        {
            get { return this.selectedSongs; }
            set { this.RaiseAndSetIfChanged(value); }
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