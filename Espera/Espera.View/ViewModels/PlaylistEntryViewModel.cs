using System;
using Espera.Core;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    public class PlaylistEntryViewModel : SongViewModelBase<PlaylistEntryViewModel>
    {
        private bool hasCachingFailed;
        private bool isInactive;
        private bool isPlaying;

        public PlaylistEntryViewModel(Song model, int index)
            : base(model)
        {
            if (index < 0)
                Throw.ArgumentOutOfRangeException(() => index, 0);

            this.Index = index;

            if (!this.Wrapped.HasToCache || this.Wrapped.IsCached)
            {
                this.CacheProgress = 100;
            }

            else
            {
                this.Wrapped.CachingProgressChanged +=
                    (sender, e) => this.CacheProgress = (int)e.ProgressPercentage;

                this.Wrapped.CachingFailed += (sender, args) => this.HasCachingFailed = true;

                this.Wrapped.CachingCompleted += (sender, e) => this.CacheProgress = 100;
            }
        }

        public int CacheProgress
        {
            get { return this.Wrapped.CachingPercentage; }
            set
            {
                if (this.CacheProgress != value)
                {
                    this.Wrapped.CachingPercentage = value;
                    this.OnPropertyChanged(vm => vm.CacheProgress);
                }
            }
        }

        public bool HasCachingFailed
        {
            get { return this.hasCachingFailed; }
            set
            {
                if (this.HasCachingFailed != value)
                {
                    this.hasCachingFailed = value;
                    this.OnPropertyChanged(vm => vm.HasCachingFailed);
                }
            }
        }

        public int Index { get; private set; }

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
    }
}