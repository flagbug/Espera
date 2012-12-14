using Espera.Core.Management;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Reactive;

namespace Espera.View.ViewModels
{
    internal sealed class StatusViewModel : ReactiveObject
    {
        private readonly Library library;
        private bool isAdding;
        private bool isUpdating;
        private string path;
        private int processedTags;
        private int totalTags;

        public StatusViewModel(Library library)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.library = library;
            this.library.Updating += (sender, e) => this.IsUpdating = true;
            this.library.Updated += (sender, args) => this.IsUpdating = false;

            this.WhenAny(x => x.IsAdding, x => Unit.Default)
                .Subscribe(p => this.RaisePropertyChanged(x => x.IsProgressUnkown));
        }

        public bool IsAdding
        {
            get { return this.isAdding; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool IsProgressUnkown
        {
            get { return this.ProcessedTags == this.TotalTags && this.IsAdding; }
        }

        public bool IsUpdating
        {
            get { return this.isUpdating; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public string Path
        {
            get { return this.path; }
            private set { this.RaiseAndSetIfChanged(value); }
        }

        public int ProcessedTags
        {
            get { return this.processedTags; }
            private set { this.RaiseAndSetIfChanged(value); }
        }

        public int TotalTags
        {
            get { return this.totalTags; }
            private set { this.RaiseAndSetIfChanged(value); }
        }

        public void Reset()
        {
            this.Path = null;
            this.ProcessedTags = 0;
            this.TotalTags = 0;
            this.IsAdding = false;
        }

        public void Update(string path, int processedTags, int totalTags)
        {
            if (path == null)
                Throw.ArgumentNullException(() => path);

            if (processedTags < 0)
                Throw.ArgumentOutOfRangeException(() => processedTags, 0);

            if (totalTags < 0)
                Throw.ArgumentOutOfRangeException(() => totalTags, 0);

            this.Path = path;
            this.ProcessedTags = processedTags;
            this.TotalTags = totalTags;

            this.RaisePropertyChanged(x => x.IsProgressUnkown);
        }
    }
}