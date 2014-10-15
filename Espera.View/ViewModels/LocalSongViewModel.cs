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
                throw new ArgumentNullException("model");

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

        public string Album
        {
            get { return this.Model.Album; }
        }

        public string Artist
        {
            get { return this.Model.Artist; }
        }

        public TimeSpan Duration
        {
            get { return this.Model.Duration; }
        }

        public string FormattedDuration
        {
            get { return this.Duration.FormatAdaptive(); }
        }

        public string Genre
        {
            get { return this.Model.Genre; }
        }

        public Song Model { get; private set; }

        public string Path
        {
            get { return this.Model.OriginalPath; }
        }

        public string Title
        {
            get { return this.Model.Title; }
        }

        public int TrackNumber
        {
            get { return this.Model.TrackNumber; }
        }

        public void Dispose()
        {
            this.propertyChangedSubscription.Dispose();
        }

        public bool Equals(LocalSongViewModel other)
        {
            return this.Model.Equals(other.Model);
        }

        public override int GetHashCode()
        {
            return this.Model.GetHashCode();
        }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}