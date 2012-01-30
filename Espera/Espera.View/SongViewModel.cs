using System;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View
{
    public class SongViewModel : ViewModelBase<SongViewModel>
    {
        private bool isPlaying;

        public Song Model { get; private set; }

        public string Album
        {
            get { return this.Model.Album; }
        }

        public string Artist
        {
            get { return this.Model.Artist; }
        }

        public TimeSpan Duration
        {
            get { return this.Model.Duration; }
        }

        public string Genre
        {
            get { return this.Model.Genre; }
        }

        public string Title
        {
            get { return this.Model.Title; }
        }

        public bool IsPlaying
        {
            get { return this.isPlaying; }
            set
            {
                if (this.IsPlaying != value)
                {
                    this.isPlaying = value;
                    this.OnPropertyChanged(vm => vm.IsPlaying);
                }
            }
        }

        public SongViewModel(Song model)
        {
            this.Model = model;
        }
    }
}