using Akavache;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
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

            return String.Compare(SortHelpers.RemoveArtistPrefixes(this.Name), SortHelpers.RemoveArtistPrefixes(other.Name), StringComparison.InvariantCultureIgnoreCase);
        }

        public bool Equals(ArtistViewModel other)
        {
            return this.Name == other.Name;
        }

        public void UpdateArtwork(IEnumerable<IObservable<string>> keys)
        {
            foreach (IObservable<string> key in keys)
            {
                this.artworkKeys.OnNext(key);
            }
        }

        

        private static async Task SaveImageToBlobCache(string key, IBitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                await bitmap.Save(CompressedBitmapFormat.Jpeg, 1, ms);

                // We don't want to wait on the disk, just fire-and-forget
                BlobCache.LocalMachine.Insert(key, ms.ToArray()).Subscribe();
            }
        }

        private async Task<BitmapSource> LoadArtworkAsync(string key)
        {
            try
            {
                await Gate.WaitAsync();

                int size = 50;
                string sizeAffix = string.Format("-{0}x{0}", size);

                // If we don't have the small version of an artwork, resize, save and return it.
                // This saves us a bunch of memory at the next startup, because BitmapImage has some
                // kind of memory leak, so the not-resized image hangs around in memory forever
                IBitmap img = await BlobCache.LocalMachine.LoadImage(key + sizeAffix)
                    .Catch(BlobCache.LocalMachine.LoadImage(key, size, size)
                    .Do(x => SaveImageToBlobCache(key + sizeAffix, x))) // We have the resized image already, so don't wait on this
                    .Finally(() => Gate.Release());

                return img.ToNative();
            }

            catch (KeyNotFoundException ex)
            {
                this.Log().WarnException("Could not find key of album cover. This reeks like a threading problem", ex);

                return null;
            }

            catch (NotSupportedException ex)
            {
                this.Log().InfoException("Unable to load artist cover", ex);

                return null;
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Akavache threw an error on artist cover loading", ex);

                return null;
            }
        }
    }
}