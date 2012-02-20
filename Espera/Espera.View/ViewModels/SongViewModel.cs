using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    public class SongViewModel : SongViewModelBase<SongViewModel>
    {
        private BitmapImage thumbnail;

        public Song Model
        {
            get { return this.Wrapped; }
        }

        public ImageSource Thumbnail
        {
            get
            {
                var song = this.Model as YoutubeSong;

                if (song != null)
                {
                    return this.thumbnail ?? (this.thumbnail = new BitmapImage(song.ThumbnailSource));
                }

                return null;
            }
        }

        public string Description
        {
            get
            {
                var song = this.Model as YoutubeSong;

                if (song != null)
                {
                    return song.Description;
                }

                return null;
            }
        }

        public string Path
        {
            get { return this.Model.Path.ToString(); }
        }

        public ICommand OpenPathCommand
        {
            get
            {
                return new RelayCommand(param => Process.Start(this.Path));
            }
        }

        public double? Rating
        {
            get
            {
                var song = this.Model as YoutubeSong;

                if (song != null && song.Rating > 0)
                {
                    return song.Rating;
                }

                return null;
            }
        }

        public SongViewModel(Song model)
            : base(model)
        { }
    }
}