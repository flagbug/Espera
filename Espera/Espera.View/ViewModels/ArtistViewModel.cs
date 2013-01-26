using ReactiveUI;

namespace Espera.View.ViewModels
{
    internal sealed class ArtistViewModel : ReactiveObject
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
            set { this.RaiseAndSetIfChanged(value); }
        }

        public int? ArtistCount
        {
            get { return this.artistCount; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool IsAllArtists { get; private set; }

        public string Name { get; private set; }

        public int? SongCount
        {
            get { return this.songCount; }
            set { this.RaiseAndSetIfChanged(value); }
        }
    }
}