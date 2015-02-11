using Espera.Core;
using Rareform.Validation;
using ReactiveUI;
using Splat;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Espera.View.ViewModels
{
    public abstract class SongViewModelBase : ReactiveObject, ISongViewModelBase
    {
        protected SongViewModelBase(Song model)
        {
            if (model == null)
                Throw.ArgumentNullException(() => model);

            this.Model = model;

            this.OpenPathCommand = ReactiveCommand.Create();
            this.OpenPathCommand.Subscribe(x =>
            {
                try
                {
                    Process.Start(this.Path);
                }

                catch (Win32Exception ex)
                {
                    this.Log().ErrorException(String.Format("Could not open link {0}", this.Path), ex);
                }
            });
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

        public ReactiveCommand<object> OpenPathCommand { get; private set; }

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