using System;
using Espera.Core;

namespace Espera.View
{
    public class SongViewModel
    {
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

        public SongViewModel(Song model)
        {
            this.Model = model;
        }
    }
}