using Espera.Core.Management;
using Rareform.Patterns.MVVM;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    internal sealed class StatusViewModel : ViewModelBase<StatusViewModel>
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
        }

        public bool IsAdding
        {
            get { return this.isAdding; }
            set
            {
                if (this.IsAdding != value)
                {
                    this.isAdding = value;
                    this.OnPropertyChanged(vm => vm.IsAdding);
                    this.OnPropertyChanged(vm => vm.IsProgressUnkown);
                }
            }
        }

        public bool IsProgressUnkown
        {
            get { return this.ProcessedTags == this.TotalTags && this.IsAdding; }
        }

        public bool IsUpdating
        {
            get { return this.isUpdating; }
            set
            {
                if (this.isUpdating != value)
                {
                    this.isUpdating = value;
                    this.OnPropertyChanged(vm => vm.IsUpdating);
                }
            }
        }

        public string Path
        {
            get { return this.path; }
            private set
            {
                if (this.Path != value)
                {
                    this.path = value;
                    this.OnPropertyChanged(vm => vm.Path);
                }
            }
        }

        public int ProcessedTags
        {
            get { return this.processedTags; }
            private set
            {
                if (this.ProcessedTags != value)
                {
                    this.processedTags = value;
                    this.OnPropertyChanged(vm => vm.ProcessedTags);
                }
            }
        }

        public int TotalTags
        {
            get { return this.totalTags; }
            private set
            {
                if (this.totalTags != value)
                {
                    this.totalTags = value;
                    this.OnPropertyChanged(vm => vm.TotalTags);
                }
            }
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

            this.OnPropertyChanged(vm => vm.IsProgressUnkown);
        }
    }
}