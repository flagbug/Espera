using System;
using Espera.Core;
using Rareform.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    public class SongViewModelBase<T> : ViewModelBase<T>
    {
        protected Song Wrapped { get; private set; }

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

        protected SongViewModelBase(Song wrapped)
        {
            this.Wrapped = wrapped;
        }
    }
}