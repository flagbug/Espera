namespace Espera.View.ViewModels
{
    internal sealed class ArtistViewModel
    {
        public ArtistViewModel(string name, int albumCount, int songCount)
        {
            this.Name = name;
            this.AlbumCount = albumCount;
            this.SongCount = songCount;
        }

        public ArtistViewModel(string name, int artistCount)
        {
            this.Name = name;
            this.ArtistCount = artistCount;
            this.IsAllArtists = true;
        }

        public int? AlbumCount { get; private set; }

        public int? ArtistCount { get; private set; }

        public bool IsAllArtists { get; private set; }

        public string Name { get; private set; }

        public int? SongCount { get; private set; }
    }
}