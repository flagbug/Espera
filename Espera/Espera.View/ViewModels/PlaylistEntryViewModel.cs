using Espera.Core;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    public sealed class PlaylistEntryViewModel : SongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<int> cachingProgress;
        private bool hasCachingFailed;
        private bool isInactive;
        private bool isPlaying;

        public PlaylistEntryViewModel(Song model, int index)
            : base(model)
        {
            if (index < 0)
                Throw.ArgumentOutOfRangeException(() => index, 0);

            this.Index = index;

            if (this.Model.HasToCache && !this.Model.IsCached)
            {
                this.cachingProgress = this.Model.CachingProgress
                    .DistinctUntilChanged()
                    .ToProperty(this, x => x.CacheProgress);

                this.Model.CachingFailed += (sender, args) => this.HasCachingFailed = true;

                this.Model.CachingCompleted += (sender, e) => this.RaisePropertyChanged(x => x.ShowCaching);
            }

            this.Model.Corrupted += (sender, args) => this.RaisePropertyChanged(x => x.IsCorrupted);
        }

        public int CacheProgress
        {
            get { return this.cachingProgress == null ? 0 : this.cachingProgress.Value; }
        }

        public bool HasCachingFailed
        {
            get { return this.hasCachingFailed; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public int Index { get; private set; }

        public bool IsCorrupted
        {
            get { return this.Model.IsCorrupted; }
        }

        public bool IsInactive
        {
            get { return this.isInactive; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool IsPlaying
        {
            get { return this.isPlaying; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool ShowCaching
        {
            get { return this.Model.HasToCache && this.CacheProgress != 100 || this.HasCachingFailed; }
        }

        public string Source
        {
            get
            {
                if (this.Model is LocalSong)
                {
                    return "Local";
                }

                if (this.Model is YoutubeSong)
                {
                    return "YouTube";
                }

                throw new InvalidOperationException();
            }
        }
    }
}