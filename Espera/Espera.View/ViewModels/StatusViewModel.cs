using FlagLib.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    internal class StatusViewModel : ViewModelBase<StatusViewModel>
    {
        private string path;
        private int processedTags;
        private int totalTags;
        private bool isAdding;

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

        public bool IsAdding
        {
            get { return this.isAdding; }
            set
            {
                if (this.IsAdding != value)
                {
                    this.isAdding = value;
                    this.OnPropertyChanged(vm => vm.IsAdding);
                }
            }
        }

        public void Update(string path, int processedTags, int totalTags)
        {
            this.Path = path;
            this.ProcessedTags = processedTags;
            this.TotalTags = totalTags;
        }
    }
}