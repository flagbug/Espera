using System;
using Espera.Core;

namespace Espera.View.ViewModels
{
    public class PlaylistEntryViewModel : SongViewModelBase<PlaylistEntryViewModel>
    {
        private bool isPlaying;
        private bool isInactive;

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

        public bool IsInactive
        {
            get { return this.isInactive; }
            set
            {
                if (this.IsInactive != value)
                {
                    this.isInactive = value;
                    this.OnPropertyChanged(vm => vm.IsInactive);
                }
            }
        }

        public string Source
        {
            get
            {
                if (this.Wrapped is LocalSong)
                {
                    return "Local";
                }

                if (this.Wrapped is YoutubeSong)
                {
                    return "YouTube";
                }

                throw new InvalidOperationException();
            }
        }

        public PlaylistEntryViewModel(Song model)
            : base(model)
        { }
    }
}