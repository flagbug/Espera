using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Espera.Core;
using ReactiveUI;
using Splat;

namespace Espera.View.ViewModels
{
    public sealed class ArtistViewModel : ReactiveObject, IEquatable<ArtistViewModel>, IDisposable
    {
        private readonly ObservableAsPropertyHelper<BitmapSource> cover;
        private readonly int orderHint;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="artistName"></param>
        /// <param name="artworkKeys"></param>
        /// <param name="orderHint">
        /// A hint that tells this instance which position it has in the artist list. This helps for
        /// priorizing the album cover loading. The higher the number, the earlier it is in the list
        /// (Think of a reversed sorted list).
        /// </param>
        public ArtistViewModel(string artistName, IObservable<string> artworkKeys, int orderHint = 1)
        {
            this.orderHint = orderHint;

            this.cover = artworkKeys
                .Where(x => x != null)
                .Distinct() // Ignore duplicate artworks
                .Select(key => Observable.FromAsync(() => this.LoadArtworkAsync(key)))
                .Concat()
                .FirstOrDefaultAsync(pic => pic != null)
                .ToProperty(this, x => x.Cover);
            var connect = this.Cover; // Connect the property to the source observable immediately

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

        public void Dispose()
        {
            this.cover?.Dispose();
        }

        public bool Equals(ArtistViewModel other)
        {
            if (Object.ReferenceEquals(other, null))
            {
                return false;
            }

            if (this.IsAllArtists && other.IsAllArtists)
            {
                return true;
            }

            if (this.IsAllArtists || other.IsAllArtists)
            {
                return false;
            }

            return this.Name.Equals(other.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj as ArtistViewModel);
        }

        public override int GetHashCode()
        {
            return new { A = this.IsAllArtists, B = this.Name }.GetHashCode();
        }

        private async Task<BitmapSource> LoadArtworkAsync(string key)
        {
            try
            {
                IBitmap img = await ArtworkCache.Instance.Retrieve(key, 50, orderHint);

                return img.ToNative();
            }

            catch (KeyNotFoundException ex)
            {
                this.Log().WarnException(String.Format("Could not find key {0} of album cover. This reeks like a threading problem", key), ex);

                return null;
            }

            catch (ArtworkCacheException ex)
            {
                this.Log().ErrorException(String.Format("Unable to load artwork with key {0} from cache", key), ex);

                return null;
            }

            catch (Exception ex)
            {
                this.Log().InfoException(String.Format("Akavache threw an error on artist cover loading for key {0}", key), ex);

                return null;
            }
        }

        /// <summary>
        /// A custom equality class for the artist grouping, until
        /// https://github.com/RolandPheasant/DynamicData/issues/31 is resolved
        /// </summary>
        public class ArtistString : IEquatable<ArtistString>
        {
            private readonly string artistName;

            public ArtistString(string artistName)
            {
                this.artistName = artistName;
            }

            public static implicit operator ArtistString(string source)
            {
                return new ArtistString(source);
            }

            public static implicit operator string(ArtistString source)
            {
                return source.artistName;
            }

            public bool Equals(ArtistString other)
            {
                return StringComparer.InvariantCultureIgnoreCase.Equals(this.artistName, other.artistName);
            }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as ArtistString);
            }

            public override int GetHashCode()
            {
                return StringComparer.InvariantCultureIgnoreCase.GetHashCode(this.artistName);
            }
        }

        public class Comparer : IComparer<ArtistViewModel>
        {
            public int Compare(ArtistViewModel x, ArtistViewModel y)
            {
                if (x.IsAllArtists && y.IsAllArtists)
                {
                    return 0;
                }

                if (x.IsAllArtists)
                {
                    return -1;
                }

                if (y.IsAllArtists)
                {
                    return 1;
                }

                return String.Compare(SortHelpers.RemoveArtistPrefixes(x.Name), SortHelpers.RemoveArtistPrefixes(y.Name), StringComparison.InvariantCultureIgnoreCase);
            }
        }
    }
}