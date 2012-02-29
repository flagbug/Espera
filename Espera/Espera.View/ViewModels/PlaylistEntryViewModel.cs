using System;
using Espera.Core;

namespace Espera.View.ViewModels
{
    public class PlaylistEntryViewModel : SongViewModelBase<PlaylistEntryViewModel>
    {
        private bool isPlaying;
        private bool isInactive;
        private int cacheProgress;

        public int CacheProgress
        {
            get { return this.cacheProgress; }
            set
            {
                if (this.CacheProgress != value)
                {
                    this.cacheProgress = value;
                    this.OnPropertyChanged(vm => vm.CacheProgress);
                }
            }
        }

        public int Index { get; private set; }

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

        public PlaylistEntryViewModel(Song model, int index)
            : base(model)
        {
            this.Index = index;

            if (this.Wrapped.IsCached)
            {
                this.CacheProgress = 100;
            }

            else
            {
                this.Wrapped.CachingProgressChanged += (sender, e) =>
                {
                    this.CacheProgress = (int)((e.TransferredBytes * 1.0 / e.TotalBytes) * 100);
                };
            }
        }
    }
}