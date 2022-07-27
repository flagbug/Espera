using System;
using Espera.Core;

namespace Espera.View.ViewModels
{
    public abstract class SongViewModelBase : ReactiveObject, ISongViewModelBase
    {
        protected SongViewModelBase(Song model)
        {
            if (model == null)
                Throw.ArgumentNullException(() => model);

            Model = model;

            OpenPathCommand = ReactiveCommand.Create();
            OpenPathCommand.Subscribe(x =>
            {
                try
                {
                    Process.Start(Path);
                }

                catch (Win32Exception ex)
                {
                    this.Log().ErrorException(string.Format("Could not open link {0}", Path), ex);
                }
            });
        }

        public ReactiveCommand<object> OpenPathCommand { get; }

        public string Album => Model.Album;

        public string Artist => Model.Artist;

        public TimeSpan Duration => Model.Duration;

        public string FormattedDuration => Duration.FormatAdaptive();

        public string Genre => Model.Genre;

        public Song Model { get; }

        public string Path => Model.OriginalPath;

        public string Title => Model.Title;

        public int TrackNumber => Model.TrackNumber;
    }
}