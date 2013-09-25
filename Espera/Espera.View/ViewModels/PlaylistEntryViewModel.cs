using Espera.Core;
using Espera.Core.Management;
using ReactiveMarrow;
using ReactiveUI;
using System;
using System.Reactive.Disposables;

namespace Espera.View.ViewModels
{
    public sealed class PlaylistEntryViewModel : SongViewModelBase, IDisposable
    {
        private readonly CompositeDisposable disposable;
        private readonly PlaylistEntry entry;
        private readonly ObservableAsPropertyHelper<bool> isCorrupted;
        private bool isPlaying;

        public PlaylistEntryViewModel(PlaylistEntry entry)
            : base(entry.Song)
        {
            this.entry = entry;

            this.disposable = new CompositeDisposable();

            this.isCorrupted = this.Model.IsCorrupted
                .ToProperty(this, x => x.IsCorrupted)
                .DisposeWith(disposable);
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