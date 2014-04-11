using System;
using Espera.Core;
using Rareform.Validation;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    public abstract class SongViewModelBase : ReactiveObject, ISongViewModelBase
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
    }
}