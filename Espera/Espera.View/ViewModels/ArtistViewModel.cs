using Caliburn.Micro;

namespace Espera.View.ViewModels
{
    internal sealed class ArtistViewModel : PropertyChangedBase
    {
        private int? albumCount;
        private int? artistCount;
        private int? songCount;

        public ArtistViewModel(string name, int albumCount, int songCount)
        {
            this.Name = name;
            this.AlbumCount = albumCount;
            this.SongCount = songCount;
        }

        public ArtistViewModel(string name)
        {
            this.Name = name;
            this.IsAllArtists = true;
        }

        public int? AlbumCount
        {
            get { return this.albumCount; }
            set
            {
                if (this.AlbumCount != value)
                {
                    this.albumCount = value;
                    this.NotifyOfPropertyChange(() => this.AlbumCount);
                }
            }
        }

        public int? ArtistCount
        {
            get { return this.artistCount; }
            set
            {
                if (this.ArtistCount != value)
                {
                    this.artistCount = value;
                    this.NotifyOfPropertyChange(() => this.ArtistCount);
                }
            }
        }

        public bool IsAllArtists { get; private set; }

        public string Name { get; private set; }

        public int? SongCount
        {
            get { return this.songCount; }
            set
            {
                if (this.SongCount != value)
                {
                    this.songCount = value;
                    this.NotifyOfPropertyChange(() => this.SongCount);
                }
            }
        }
    }
}