using Caliburn.Micro;
using Espera.Core;
using Rareform.Validation;
using System;

namespace Espera.View.ViewModels
{
    public abstract class SongViewModelBase : PropertyChangedBase
    {
        protected SongViewModelBase(Song wrapped)
        {
            if (wrapped == null)
                Throw.ArgumentNullException(() => wrapped);

            this.Wrapped = wrapped;
        }

        public string Album
        {
            get { return this.Wrapped.Album; }
        }

        public string Artist
        {
            get { return this.Wrapped.Artist; }
        }

        public TimeSpan Duration
        {
            get { return this.Wrapped.Duration; }
        }

        public string Genre
        {
            get { return this.Wrapped.Genre; }
        }

        public string Title
        {
            get { return this.Wrapped.Title; }
        }

        public int TrackNumber
        {
            get { return this.Wrapped.TrackNumber; }
        }

        protected Song Wrapped { get; private set; }
    }
}