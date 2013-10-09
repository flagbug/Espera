using Akavache;
using MoreLinq;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Espera.View.ViewModels
{
    public sealed class ArtistViewModel : ReactiveObject, IComparable<ArtistViewModel>, IEquatable<ArtistViewModel>
    {
        private static readonly SemaphoreSlim Gate; // This gate is used to limit the I/O access when accessing the artwork cache to 1 item at a time
        private readonly Subject<IObservable<string>> artworkKeys;
        private readonly ObservableAsPropertyHelper<BitmapSource> cover;

        static ArtistViewModel()
        {
            Gate = new SemaphoreSlim(1, 1);
        }

        public ArtistViewModel(string artistName, IEnumerable<IObservable<string>> artworkKeys)
        {
            this.artworkKeys = new Subject<IObservable<string>>();

            this.cover = this.artworkKeys
                .Merge()
                .Where(x => x != null)
                .Distinct() // Ignore duplicate artworks
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(key => LoadArtworkAsync(key).Result)
                .FirstOrDefaultAsync(pic => pic != null)
                .ToProperty(this, x => x.Cover);

            this.UpdateArtwork(artworkKeys);

            this.Name = artistName;
            this.IsAllArtists = false;
        }

        public ArtistViewModel(string allArtistsName)
        {
            this.Name = allArtistsName;
            this.IsAllArtists = true;
        }

        public BitmapSource Cover
        {
            get { return this.cover == null ? null : this.cover.Value; }
        }

        public bool IsAllArtists { get; private set; }

        public string Name { get; private set; }

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

        public bool Equals(ArtistViewModel other)
        {
            return this.Name == other.Name;
        }

        public void UpdateArtwork(IEnumerable<IObservable<string>> keys)
        {
            keys.ForEach(x => this.artworkKeys.OnNext(x));
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