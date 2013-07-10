using Espera.Core;
using Espera.Core.Management;
using ReactiveMarrow;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    public sealed class PlaylistEntryViewModel : SongViewModelBase, IDisposable
    {
        private readonly ObservableAsPropertyHelper<int> cachingProgress;
        private readonly CompositeDisposable disposable;
        private readonly PlaylistEntry entry;
        private readonly ObservableAsPropertyHelper<bool> hasCachingFailed;
        private readonly ObservableAsPropertyHelper<bool> hasStreamingFailed;
        private readonly ObservableAsPropertyHelper<bool> isCorrupted;
        private readonly ObservableAsPropertyHelper<bool> showCaching;
        private bool isPlaying;

        public PlaylistEntryViewModel(PlaylistEntry entry)
            : base(entry.Song)
        {
            this.entry = entry;

            this.disposable = new CompositeDisposable();

            // This check greatly decreases the memory footprint of the application,
            // since the observables are only created for songs that actually have to be cached
            if (this.Model.HasToCache)
            {
                this.cachingProgress = this.Model.PreparationProgress
                    .DistinctUntilChanged()
                    .ToProperty(this, x => x.CacheProgress)
                    .DisposeWith(this.disposable);

                this.showCaching = this.Model.PreparationCompleted.StartWith(Unit.Default)
                    .CombineLatest(this.Model.PreparationProgress.DistinctUntilChanged(), (unit, progress) => progress)
                    .Select(progress => this.Model.HasToCache && progress != 100 || this.HasCachingFailed)
                    .ToProperty(this, x => x.ShowCaching)
                    .DisposeWith(this.disposable);

                this.hasCachingFailed = this.Model.PreparationFailed
                    .Select(x => x == PreparationFailureCause.CachingFailed)
                    .ToProperty(this, x => x.HasCachingFailed)
                    .DisposeWith(this.disposable);
            }

            if (this.Model is YoutubeSong)
            {
                this.hasStreamingFailed = this.Model.PreparationFailed
                    .Select(x => x == PreparationFailureCause.StreamingFailed)
                    .ToProperty(this, x => x.HasStreamingFailed)
                    .DisposeWith(this.disposable);
            }

            this.isCorrupted = this.Model.IsCorrupted
                .ToProperty(this, x => x.IsCorrupted)
                .DisposeWith(disposable);
        }

        public int CacheProgress
        {
            get { return this.cachingProgress != null ? this.cachingProgress.Value : -1; }
        }

        public bool HasCachingFailed
        {
            get { return this.hasCachingFailed != null && this.hasCachingFailed.Value; }
        }

        public bool HasStreamingFailed
        {
            get { return this.hasStreamingFailed != null && this.hasStreamingFailed.Value; }
        }

        public int Index
        {
            get { return this.entry.Index; }
        }

        public bool IsCorrupted
        {
            get { return this.isCorrupted.Value; }
        }

        public bool IsPlaying
        {
            get { return this.isPlaying; }
            set { this.RaiseAndSetIfChanged(ref this.isPlaying, value); }
        }

        public bool ShowCaching
        {
            get { return this.showCaching != null && this.showCaching.Value; }
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

        public void Dispose()
        {
            this.disposable.Dispose();
        }
    }
}