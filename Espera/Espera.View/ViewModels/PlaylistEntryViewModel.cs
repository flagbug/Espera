using Espera.Core;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    public sealed class PlaylistEntryViewModel : SongViewModelBase
    {
        private readonly ObservableAsPropertyHelper<int> cachingProgress;
        private readonly ObservableAsPropertyHelper<bool> hasCachingFailed;
        private readonly ObservableAsPropertyHelper<bool> showCaching;
        private bool isInactive;
        private bool isPlaying;

        public PlaylistEntryViewModel(Song model, int index)
            : base(model)
        {
            if (index < 0)
                Throw.ArgumentOutOfRangeException(() => index, 0);

            this.Index = index;

            this.cachingProgress = this.Model.CachingProgress
                .DistinctUntilChanged()
                .ToProperty(this, x => x.CacheProgress);

            this.hasCachingFailed = this.Model.CachingFailed.Select(x => true)
                .ToProperty(this, x => x.HasCachingFailed);

            this.showCaching = this.Model.CachingCompleted.StartWith(Unit.Default)
                .CombineLatest(this.Model.CachingProgress.DistinctUntilChanged(), (unit, progress) => progress)
                .Select(progress => this.Model.HasToCache && progress != 100 || this.HasCachingFailed)
                .ToProperty(this, x => x.ShowCaching);

            this.Model.Corrupted.Subscribe(x => this.RaisePropertyChanged(p => p.IsCorrupted));
        }

        public int CacheProgress
        {
            get { return this.cachingProgress.Value; }
        }

        public bool HasCachingFailed
        {
            get { return this.hasCachingFailed.Value; }
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
            get { return this.showCaching.Value; }
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