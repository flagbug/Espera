using Espera.Core;
using Espera.Core.Helpers;
using Espera.Core.Management;
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
        private readonly ObservableAsPropertyHelper<bool> showCaching;
        private bool isPlaying;

        public PlaylistEntryViewModel(PlaylistEntry entry)
            : base(entry.Song)
        {
            this.entry = entry;

            this.disposable = new CompositeDisposable();

            this.cachingProgress = this.Model.CachingProgress
                .DistinctUntilChanged()
                .ToProperty(this, x => x.CacheProgress);

            this.hasCachingFailed = this.Model.CachingFailed.Select(x => true)
                .ToProperty(this, x => x.HasCachingFailed);

            this.showCaching = this.Model.CachingCompleted.StartWith(Unit.Default)
                .CombineLatest(this.Model.CachingProgress.DistinctUntilChanged(), (unit, progress) => progress)
                .Select(progress => this.Model.HasToCache && progress != 100 || this.HasCachingFailed)
                .ToProperty(this, x => x.ShowCaching);

            this.Model.Corrupted.Subscribe(x => this.RaisePropertyChanged(p => p.IsCorrupted)).DisposeWith(disposable);
        }

        public int CacheProgress
        {
            get { return this.cachingProgress.Value; }
        }

        public bool HasCachingFailed
        {
            get { return this.hasCachingFailed.Value; }
        }

        public int Index
        {
            get { return this.entry.Index; }
        }

        public bool IsCorrupted
        {
            get { return this.Model.IsCorrupted; }
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

        public void Dispose()
        {
            this.cachingProgress.Dispose();
            this.hasCachingFailed.Dispose();
            this.showCaching.Dispose();
            this.disposable.Dispose();
        }
    }
}