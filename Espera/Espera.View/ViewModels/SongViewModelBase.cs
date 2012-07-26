using System;
using Espera.Core;
using Rareform.Patterns.MVVM;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    public abstract class SongViewModelBase<T> : ViewModelBase<T>
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

        protected Song Wrapped { get; private set; }
    }
}