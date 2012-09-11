using Caliburn.Micro;
using Espera.Core.Management;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    internal sealed class StatusViewModel : PropertyChangedBase
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
                    this.NotifyOfPropertyChange(() => this.IsAdding);
                    this.NotifyOfPropertyChange(() => this.IsProgressUnkown);
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
                    this.NotifyOfPropertyChange(() => this.IsUpdating);
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
                    this.NotifyOfPropertyChange(() => this.Path);
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
                    this.NotifyOfPropertyChange(() => this.ProcessedTags);
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
                    this.NotifyOfPropertyChange(() => this.TotalTags);
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

            this.NotifyOfPropertyChange(() => this.IsProgressUnkown);
        }
    }
}