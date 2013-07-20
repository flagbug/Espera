using Akavache;
using Espera.Core;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Espera.View.ViewModels
{
    internal sealed class ArtistViewModel : ReactiveObject, IComparable<ArtistViewModel>, IEquatable<ArtistViewModel>, IDisposable
    {
        private readonly ObservableAsPropertyHelper<BitmapSource> cover;
        private IEnumerable<LocalSongViewModel> songs;

        public ArtistViewModel(IEnumerable<LocalSongViewModel> songs)
            : this(songs.First().Artist)
        {
            this.Songs = songs.ToList();

            List<string> keys = this.Songs
                .Select(x => x.Model)
                .Cast<LocalSong>()
                .Where(x => x.AlbumCoverKey != null)
                .Select(x => x.AlbumCoverKey)
                .ToList();

            this.cover = Observable.StartAsync(() => LoadCoverAsync(keys))
                .ToProperty(this, x => x.Cover);
        }

        public ArtistViewModel(string name)
        {
            this.Name = name;
        }

        public BitmapSource Cover
        {
            get { return this.cover == null ? null : this.cover.Value; }
        }

        public bool IsAllArtists
        {
            get { return this.Songs == null; }
        }

        public string Name { get; private set; }

        public IEnumerable<LocalSongViewModel> Songs
        {
            get { return this.songs; }
            set { this.RaiseAndSetIfChanged(ref this.songs, value); }
        }

        public int CompareTo(ArtistViewModel other)
        {
            if (this.IsAllArtists && other.IsAllArtists)
            {
                return 0;
            }

            if (this.IsAllArtists)
            {
                return -1;
            }

            if (other.IsAllArtists)
            {
                return 1;
            }

            var prefixes = new[] { "A", "The" };

            return String.Compare(RemoveArtistPrefixes(this.Name, prefixes), RemoveArtistPrefixes(other.Name, prefixes), StringComparison.Ordinal);
        }

        public void Dispose()
        {
            if (this.cover != null)
            {
                this.cover.Dispose();
            }
        }

        public bool Equals(ArtistViewModel other)
        {
            return this.Name == other.Name;
        }

        private static async Task<BitmapSource> LoadCoverAsync(IEnumerable<string> availableKeys)
        {
            foreach (string key in availableKeys)
            {
                byte[] imageBytes;

                try
                {
                    imageBytes = await BlobCache.LocalMachine.GetAsync(key);
                }

                catch (KeyNotFoundException)
                {
                    continue;
                }

                using (var imageStream = new MemoryStream(imageBytes))
                {
                    var image = new BitmapImage();

                    image.BeginInit();
                    image.StreamSource = imageStream;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelHeight = 35;
                    image.DecodePixelWidth = 35;

                    try
                    {
                        image.EndInit();
                    }

                    catch (NotSupportedException)
                    {
                        continue;
                    }

                    image.Freeze();

                    return image;
                }
            }

            return null;
        }

        /// <example>
        /// With prefixes "A" and "The":
        /// "A Bar" -> "Bar", "The Foos" -> "Foos"
        /// </example>
        private static string RemoveArtistPrefixes(string artistName, IEnumerable<string> prefixes)
        {
            foreach (string s in prefixes)
            {
                int lengthWithSpace = s.Length + 1;

                if (artistName.Length >= lengthWithSpace && artistName.Substring(0, lengthWithSpace) == s + " ")
                {
                    return artistName.Substring(lengthWithSpace);
                }
            }

            return artistName;
        }
    }
}