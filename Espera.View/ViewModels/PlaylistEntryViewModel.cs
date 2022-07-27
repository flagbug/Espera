using System;
using System.Reactive.Disposables;
using Espera.Core;
using Espera.Core.Management;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public sealed class PlaylistEntryViewModel : SongViewModelBase, IDisposable
    {
        private readonly CompositeDisposable disposable;
        private readonly PlaylistEntry entry;
        private readonly ObservableAsPropertyHelper<bool> isCorrupted;
        private readonly ObservableAsPropertyHelper<int> votes;
        private bool isPlaying;

        public PlaylistEntryViewModel(PlaylistEntry entry)
            : base(entry.Song)
        {
            this.entry = entry;

            disposable = new CompositeDisposable();

            isCorrupted = Model.WhenAnyValue(x => x.IsCorrupted)
                .ToProperty(this, x => x.IsCorrupted)
                .DisposeWith(disposable);

            votes = this.entry.WhenAnyValue(x => x.Votes)
                .ToProperty(this, x => x.Votes)
                .DisposeWith(disposable);
        }

        public int Index => entry.Index;

        public bool IsCorrupted => isCorrupted.Value;

        public bool IsPlaying
        {
            get => isPlaying;
            set => this.RaiseAndSetIfChanged(ref isPlaying, value);
        }

        public bool IsYoutube => Model is YoutubeSong;

        public int Votes => votes.Value;

        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}