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
    internal abstract class SongSourceViewModel : PropertyChangedBase, ISongSourceViewModel
    {
        private readonly Library library;
        private IEnumerable<SongViewModel> selectableSongs;
        private IEnumerable<SongViewModel> selectedSongs;

        protected SongSourceViewModel(Library library)
        {
            this.library = library;
            this.SelectableSongs = Enumerable.Empty<SongViewModel>();
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

        public bool IsParty
        {
            get { return this.library.AccessMode == AccessMode.Party; }
        }

        public bool IsSongSelected
        {
            get { return this.SelectedSongs != null && this.SelectedSongs.Any(); }
        }

        public abstract string SearchText { get; set; }

        public IEnumerable<SongViewModel> SelectableSongs
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

        public IEnumerable<SongViewModel> SelectedSongs
        {
            get { return this.selectedSongs; }
            set
            {
                if (this.selectedSongs != value)
                {
                    this.selectedSongs = value;
                    this.NotifyOfPropertyChange(() => this.SelectedSongs);
                    this.NotifyOfPropertyChange(() => this.IsSongSelected);
                }
            }
        }

        protected Library Library
        {
            get { return this.library; }
        }

        protected Func<IEnumerable<SongViewModel>, IOrderedEnumerable<SongViewModel>> SongOrderFunc { get; set; }

        protected void ApplyOrder()
        {
            this.SelectableSongs = this.SongOrderFunc(this.SelectableSongs);
        }

        protected abstract void UpdateSelectableSongs();
    }
}