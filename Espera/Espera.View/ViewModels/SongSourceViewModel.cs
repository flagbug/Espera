using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Espera.Core.Management;
using Rareform.Extensions;
using Rareform.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    internal abstract class SongSourceViewModel<T> : ViewModelBase<T>, ISongSourceViewModel
        where T : SongSourceViewModel<T>
    {
        private readonly Library library;
        private IEnumerable<SongViewModel> selectableSongs;
        private IEnumerable<SongViewModel> selectedSongs;

        protected SongSourceViewModel(Library library)
        {
            this.library = library;
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
                    this.OnPropertyChanged(vm => vm.SelectableSongs);
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
                    this.OnPropertyChanged(vm => vm.SelectedSongs);
                    this.OnPropertyChanged(vm => vm.IsSongSelected);
                }
            }
        }

        protected Library Library
        {
            get { return this.library; }
        }

        protected abstract void UpdateSelectableSongs();
    }
}