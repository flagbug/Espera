using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View
{
    internal class MainViewModel : ViewModelBase<MainViewModel>
    {
        private readonly Library library;
        private string selectedArtist;

        public IEnumerable<string> Artists
        {
            get
            {
                return this.library.Songs
                    .GroupBy(song => song.Artist)
                    .Select(group => group.Key)
                    .OrderBy(artist => artist);
            }
        }

        public string SelectedArtist
        {
            get { return this.selectedArtist; }
            set
            {
                if (this.SelectedArtist != value)
                {
                    this.selectedArtist = value;
                    this.OnPropertyChanged(vm => vm.SelectedArtist);
                    this.OnPropertyChanged(vm => vm.SelectableSongs);
                }
            }
        }

        public IEnumerable<Song> SelectableSongs
        {
            get
            {
                return this.library.Songs
                    .Where(song => song.Artist == this.SelectedArtist);
            }
        }

        public TimeSpan TotalTime
        {
            get { return this.library.TotalTime; }
        }

        public TimeSpan CurrentTime
        {
            get { return this.library.CurrentTime; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel()
        {
            this.library = new Library();
        }

        public void AddSongs(string folderPath)
        {
            Task.Factory
                .StartNew(() => this.library.AddLocalSongs(folderPath))
                .ContinueWith(task => this.OnPropertyChanged(vm => vm.Artists));
        }
    }
}