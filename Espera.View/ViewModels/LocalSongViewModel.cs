using System;
using Espera.Core;

namespace Espera.View.ViewModels
{
    public sealed class LocalSongViewModel : ISongViewModelBase, IEquatable<LocalSongViewModel>, INotifyPropertyChanged,
        IDisposable
    {
        private readonly IDisposable propertyChangedSubscription;

        public LocalSongViewModel(Song model)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            Model = model;

            // We only want to know if the whole metadata changes (e.g when editing the tags in the
            // tag editor) so we can update the viewmodel, everything else isn't interesting to us
            propertyChangedSubscription = Observable
                .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    h => Model.PropertyChanged += h,
                    h => Model.PropertyChanged -= h)
                .Where(x => string.IsNullOrEmpty(x.EventArgs.PropertyName))
                .Subscribe(_ => OnPropertyChanged(string.Empty));
        }

        public void Dispose()
        {
            propertyChangedSubscription.Dispose();
        }

        public bool Equals(LocalSongViewModel other)
        {
            return Model.Equals(other.Model);
        }

        public string Album => Model.Album;

        public string Artist => Model.Artist;

        public TimeSpan Duration => Model.Duration;

        public string FormattedDuration => Duration.FormatAdaptive();

        public string Genre => Model.Genre;

        public Song Model { get; }

        public string Path => Model.OriginalPath;

        public string Title => Model.Title;

        public int TrackNumber => Model.TrackNumber;

        public event PropertyChangedEventHandler PropertyChanged;

        public override int GetHashCode()
        {
            return Model.GetHashCode();
        }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}