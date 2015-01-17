using Espera.Core;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using Splat;

namespace Espera.View.ViewModels
{
    public abstract class SongViewModelBase : ReactiveObject, ISongViewModelBase
    {
        protected SongViewModelBase(Song model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

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
                    this.Log().ErrorException($"Could not open link {this.Path}", ex);
                }
            });
        }

        public string Album => this.Model.Album;

        public string Artist => this.Model.Artist;

        public TimeSpan Duration => this.Model.Duration;

        public string FormattedDuration => this.Duration.FormatAdaptive();

        public string Genre => this.Model.Genre;

        public Song Model { get; }

        public ReactiveCommand<object> OpenPathCommand { get; }

        public string Path => this.Model.OriginalPath;

        public string Title => this.Model.Title;

        public int TrackNumber => this.Model.TrackNumber;
    }
}