using System;
using Espera.Core;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    public sealed class PlaylistEntryViewModel : SongViewModelBase<PlaylistEntryViewModel>
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

            if (this.Wrapped.HasToCache && !this.Wrapped.IsCached)
            {
                this.Wrapped.CachingProgressChanged += (sender, e) => this.OnPropertyChanged(vm => vm.CacheProgress);

                this.Wrapped.CachingFailed += (sender, args) => this.HasCachingFailed = true;

                this.Wrapped.CachingCompleted += (sender, e) => this.OnPropertyChanged(vm => vm.ShowCaching);
            }
        }

        public int CacheProgress
        {
            get { return this.Wrapped.CachingProgress; }
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

        public bool ShowCaching
        {
            get { return this.Wrapped.HasToCache && this.CacheProgress != 100 || this.HasCachingFailed; }
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