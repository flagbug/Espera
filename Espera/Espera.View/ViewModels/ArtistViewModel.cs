using Akavache;
using Espera.Core;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Espera.View.ViewModels
{
    internal sealed class ArtistViewModel : ReactiveObject, IComparable<ArtistViewModel>, IEquatable<ArtistViewModel>, IDisposable, IEnableLogger
    {
        private static readonly SemaphoreSlim ArtworkLock; // This semaphore is used to limit the I/O access when accessing the artwork cache to 1 item at a time
        private readonly ObservableAsPropertyHelper<BitmapSource> cover;
        private IEnumerable<LocalSongViewModel> songs;

        static ArtistViewModel()
        {
            ArtworkLock = new SemaphoreSlim(1, 1);
        }

        public ArtistViewModel(IEnumerable<LocalSongViewModel> songs)
            : this(songs.First().Artist)
        {
            this.Songs = songs.ToList();

            this.cover = this.Songs
                .Select(x => x.Model)
                .Cast<LocalSong>()
                .Select(song => song.ArtworkKey)
                .Merge()
                .Where(key => key != null)
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

        private async Task<BitmapSource> LoadArtworkAsync(string key)
        {
            byte[] imageBytes;

            await ArtworkLock.WaitAsync(); // Limit the I/O access

            try
            {
                imageBytes = await BlobCache.LocalMachine.GetAsync(key);
            }

            catch (KeyNotFoundException)
            {
                return null;
            }

            finally
            {
                ArtworkLock.Release();
            }

            return await Task.Run(() =>
            {
                using (var imageStream = new MemoryStream(imageBytes))
                {
                    var img = new BitmapImage();

                    img.BeginInit();
                    img.StreamSource = imageStream;
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.DecodePixelHeight = 35;
                    img.DecodePixelWidth = 35;

                    try
                    {
                        img.EndInit();
                    }

                    catch (NotSupportedException ex)
                    {
                        this.Log().Info("Unable to load artist cover: {0}", ex.Message);

                        return null;
                    }

                    img.Freeze();

                    return img;
                }
            });
        }
    }
}