using Caliburn.Micro;
using Espera.Core;
using Rareform.Validation;
using System;

namespace Espera.View.ViewModels
{
    public abstract class SongViewModelBase : PropertyChangedBase
    {
        protected SongViewModelBase(Song model)
        {
            if (model == null)
                Throw.ArgumentNullException(() => model);

            this.Model = model;
        }

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

        public string Genre
        {
            get { return this.Model.Genre; }
        }

        public Song Model { get; private set; }

        public string Title
        {
            get { return this.Model.Title; }
        }

        public int TrackNumber
        {
            get { return this.Model.TrackNumber; }
        }
    }
}