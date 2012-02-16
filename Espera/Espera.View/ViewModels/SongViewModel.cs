using System;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    public class SongViewModel : ViewModelBase<SongViewModel>
    {
        private bool isPlaying;
        private BitmapImage thumbnail;

        public Song Model { get; private set; }

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

        public string Title
        {
            get { return this.Model.Title; }
        }

        public bool IsPlaying
        {
            get { return this.isPlaying; }
            set
            {
                if (this.IsPlaying != value)
                {
                    this.isPlaying = value;
                    this.OnPropertyChanged(vm => vm.IsPlaying);
                }
            }
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

        public SongViewModel(Song model)
        {
            this.Model = model;
        }
    }
}