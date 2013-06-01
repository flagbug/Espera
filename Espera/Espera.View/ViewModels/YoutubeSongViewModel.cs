using Espera.Core;
using Rareform.Patterns.MVVM;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Espera.View.ViewModels
{
    internal sealed class YoutubeSongViewModel : SongViewModelBase
    {
        private BitmapImage thumbnail;

        public YoutubeSongViewModel(YoutubeSong wrapped)
            : base(wrapped)
        { }

        public string Description
        {
            get
            {
                var song = (YoutubeSong)this.Model;

                return song.Description;
            }
        }

        public bool HasThumbnail
        {
            get { return this.thumbnail != null; }
        }

        public ICommand OpenPathCommand
        {
            get { return new RelayCommand(param => Process.Start(this.Path)); }
        }

        public double? Rating
        {
            get
            {
                var song = (YoutubeSong)this.Model;

                return song.Rating;
            }
        }

        public ImageSource Thumbnail
        {
            get
            {
                if (this.thumbnail == null)
                {
                    this.GetThumbnail();
                }

                return this.thumbnail;
            }
        }

        public int ViewCount
        {
            get { return ((YoutubeSong)this.Model).Views; }
        }

        public string Views
        {
            get
            {
                var song = (YoutubeSong)this.Model;

                return String.Format("{0:N0}", song.Views);
            }
        }

        private void GetThumbnail()
        {
            var worker = new BackgroundWorker();

            worker.DoWork += (s, e) =>
            {
                var uri = (Uri)e.Argument;

                using (var webClient = new WebClient())
                {
                    try
                    {
                        byte[] imageBytes = webClient.DownloadData(uri);

                        if (imageBytes == null)
                        {
                            e.Result = null;
                            return;
                        }

                        using (var imageStream = new MemoryStream(imageBytes))
                        {
                            var image = new BitmapImage();

                            image.BeginInit();
                            image.StreamSource = imageStream;
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.EndInit();

                            image.Freeze();

                            e.Result = image;
                        }
                    }

                    catch (WebException ex)
                    {
                        e.Result = ex;
                    }
                }
            };

            worker.RunWorkerCompleted += (s, e) =>
            {
                var bitmapImage = e.Result as BitmapImage;

                if (bitmapImage != null)
                {
                    this.thumbnail = bitmapImage;
                    this.RaisePropertyChanged("Thumbnail");
                    this.RaisePropertyChanged("HasThumbnail");
                }

                worker.Dispose();
            };

            worker.RunWorkerAsync(((YoutubeSong)this.Model).ThumbnailSource);
        }
    }
}