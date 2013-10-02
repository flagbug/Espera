using Akavache;
using Espera.Core;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Espera.View.ViewModels
{
    public sealed class ArtistViewModel : ReactiveObject, IComparable<ArtistViewModel>, IEquatable<ArtistViewModel>, IDisposable
    {
        private static readonly SemaphoreSlim Gate; // This gate is used to limit the I/O access when accessing the artwork cache to 1 item at a time
        private readonly ObservableAsPropertyHelper<BitmapSource> cover;
        private IEnumerable<LocalSongViewModel> songs;

        static ArtistViewModel()
        {
            Gate = new SemaphoreSlim(1, 1);
        }

        public ArtistViewModel(IEnumerable<LocalSongViewModel> songs)
            : this(songs.First().Artist)
        {
            this.Songs = songs.ToList();

            this.cover = this.Songs
                .Select(x => x.Model)
                .Cast<LocalSong>()
                .Select(song => song.ArtworkKey.Where(x => x != null))
                .Merge()
                .Distinct() // Ignore duplicate artworks
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(key => LoadArtworkAsync(key).Result)
                .FirstOrDefaultAsync(pic => pic != null)
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

            return String.Compare(RemoveArtistPrefixes(this.Name, prefixes), RemoveArtistPrefixes(other.Name, prefixes), StringComparison.InvariantCultureIgnoreCase);
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

        /// <example>
        /// With prefixes "A" and "The":
        /// "A Bar" -> "Bar", "The Foos" -> "Foos"
        /// </example>
        private static string RemoveArtistPrefixes(string artistName, IEnumerable<string> prefixes)
        {
            foreach (string s in prefixes)
            {
                int lengthWithSpace = s.Length + 1;

                if (artistName.Length >= lengthWithSpace && artistName.Substring(0, lengthWithSpace).Equals(s + " ", StringComparison.InvariantCultureIgnoreCase))
                {
                    return artistName.Substring(lengthWithSpace);
                }
            }

            return artistName;
        }

        private async Task<BitmapSource> LoadArtworkAsync(string key)
        {
            try
            {
                await Gate.WaitAsync();

                IBitmap img = await BlobCache.LocalMachine.LoadImage(key, 35, 35)
                    .Finally(() => Gate.Release());

                return img.ToNative();
            }

            catch (KeyNotFoundException ex)
            {
                this.Log().Warn("Could not find key of album cover. This reeks like a threading problem: {0}", ex.Message);

                return null;
            }

            catch (NotSupportedException ex)
            {
                this.Log().Info("Unable to load artist cover: {0}", ex.Message);

                return null;
            }
        }
    }
}