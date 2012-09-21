namespace Espera.View.ViewModels
{
    internal sealed class ArtistViewModel
    {
        public ArtistViewModel(string name, int? songCount, int? artistCount = null)
        {
            this.Name = name;
            this.SongCount = songCount;

            if (artistCount != null)
            {
                this.IsAllArtists = true;
                this.ArtistCount = artistCount;
            }
        }

        public int? ArtistCount { get; private set; }

        public bool IsAllArtists { get; private set; }

        public string Name { get; private set; }

        public int? SongCount { get; private set; }
    }
}