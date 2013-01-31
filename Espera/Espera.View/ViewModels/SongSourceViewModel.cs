using Caliburn.Micro;
using Espera.Core.Management;
using Rareform.Extensions;
using Rareform.Patterns.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal abstract class SongSourceViewModel<T> : PropertyChangedBase, ISongSourceViewModel
        where T : SongViewModelBase
    {
        private readonly Library library;
        private IEnumerable<T> selectableSongs;
        private IEnumerable<SongViewModelBase> selectedSongs;

        protected SongSourceViewModel(Library library)
        {
            this.library = library;
            this.selectableSongs = Enumerable.Empty<T>();
        }

        public event EventHandler TimeoutWarning;

        public ICommand AddToPlaylistCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
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
                    },
                    param => this.SelectedSongs != null && this.SelectedSongs.Any()
                );
            }
        }

        public bool IsAdmin
        {
            get { return this.library.AccessMode == AccessMode.Administrator; }
        }

        public abstract string SearchText { get; set; }

        public IEnumerable<T> SelectableSongs
        {
            get { return this.selectableSongs; }
            protected set
            {
                if (this.selectableSongs != value)
                {
                    this.selectableSongs = value;
                    this.NotifyOfPropertyChange(() => this.SelectableSongs);
                }
            }
        }

        public IEnumerable<SongViewModelBase> SelectedSongs
        {
            get { return this.selectedSongs; }
            set
            {
                if (this.selectedSongs != value)
                {
                    this.selectedSongs = value;
                    this.NotifyOfPropertyChange(() => this.SelectedSongs);
                }
            }
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