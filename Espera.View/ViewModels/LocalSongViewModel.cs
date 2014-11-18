using System;
using System.ComponentModel;
using System.Reactive.Linq;
using Espera.Core;

namespace Espera.View.ViewModels
{
    public sealed class LocalSongViewModel : ISongViewModelBase, IEquatable<LocalSongViewModel>, INotifyPropertyChanged, IDisposable
    {
        private readonly IDisposable propertyChangedSubscription;

        public LocalSongViewModel(Song model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            this.Model = model;

            // We only want to know if the whole metadata changes (e.g when editing the tags in the
            // tag editor) so we can update the viewmodel, everything else isn't interesting to us
            this.propertyChangedSubscription = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    h => this.Model.PropertyChanged += h,
                    h => this.Model.PropertyChanged -= h)
                .Where(x => string.IsNullOrEmpty(x.EventArgs.PropertyName))
                .Subscribe(_ => this.OnPropertyChanged(string.Empty));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Album => this.Model.Album;

        public string Artist => this.Model.Artist;

        public TimeSpan Duration => this.Model.Duration;

        public string FormattedDuration => this.Duration.FormatAdaptive();

        public string Genre => this.Model.Genre;

        public Song Model { get; }

        public string Path => this.Model.OriginalPath;

        public string Title => this.Model.Title;

        public int TrackNumber => this.Model.TrackNumber;

        public void Dispose() => this.propertyChangedSubscription.Dispose();

        public bool Equals(LocalSongViewModel other) => this.Model.Equals(other.Model);

        public override bool Equals(object obj) => base.Equals(obj as LocalSongViewModel);

        public override int GetHashCode() => this.Model.GetHashCode();

        private void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}