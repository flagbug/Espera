using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Akavache;
using Punchclock;
using ReactiveUI;
using Splat;

namespace Espera.Core
{
    /// <summary>
    /// A smart cache for artworks that prioritizes the retrieving of artworks as well as
    /// prioritizing the retrieval of artwork with a priority number.
    /// </summary>
    public class ArtworkCache : IEnableLogger
    {
        private static readonly Lazy<ArtworkCache> instance;
        private readonly MusicBrainzArtworkFetcher artworkFetcher;
        private readonly IBlobCache cache;
        private readonly HashSet<string> keyCache;
        private readonly Dictionary<string, AsyncSubject<Unit>> keyedSemaphore;
        private readonly OperationQueue queue;

        static ArtworkCache()
        {
            instance = new Lazy<ArtworkCache>(() => new ArtworkCache());
        }

        public ArtworkCache(IBlobCache cache = null)
        {
            this.cache = cache ?? BlobCache.LocalMachine;

            this.queue = new OperationQueue(1); // Disk operations should be serialized
            this.keyCache = new HashSet<string>();
            this.artworkFetcher = new MusicBrainzArtworkFetcher();
            this.keyedSemaphore = new Dictionary<string, AsyncSubject<Unit>>();
        }

        public static ArtworkCache Instance
        {
            get { return instance.Value; }
        }

        /// <summary>
        /// Fetches an artwork for the specified combination of artist and album.
        /// </summary>
        /// <returns>The fetched artwork, or <c>null</c>, if no artwork could be found.</returns>
        /// <exception cref="ArtworkFetchException">An error occured while fetching the artwork.</exception>
        public async Task<IBitmap> FetchOnline(string artist, string album, int size)
        {
            if (artist == null)
                throw new ArgumentNullException("artist");

            if (album == null)
                throw new ArgumentNullException("album");

            if (size < 0)
                throw new ArgumentOutOfRangeException("size");

            string lookupKey = BlobCacheKeys.GetKeyForOnlineArtwork(artist, album);

            AsyncSubject<Unit> semaphore;
            bool semaphoreExists = false;

            // Requests with the same lookup key have to wait on the first and then get the cached
            // artwork key. That won't happen often, but when it does, we are save.
            lock (this.keyedSemaphore)
            {
                if (this.keyedSemaphore.TryGetValue(lookupKey, out semaphore))
                {
                    semaphoreExists = true;
                }

                else
                {
                    semaphore = new AsyncSubject<Unit>();
                    this.keyedSemaphore.Add(lookupKey, semaphore);
                }
            }

            if (semaphoreExists)
            {
                await semaphore;
            }

            string artworkCacheKey = null;

            try
            {
                // Each lookup key gets an artwork key assigned, let's see if it's already in the cache
                artworkCacheKey = await this.cache.GetObjectAsync<string>(lookupKey);
            }

            catch (KeyNotFoundException)
            {
                this.Log().Info("Online artwork for {0} - {1} isn't in the cache", artist, album);
            }

            if (artworkCacheKey != null)
            {
                // We already have the artwork cached? Great!

                if (!semaphoreExists)
                {
                    semaphore.OnNext(Unit.Default);
                    semaphore.OnCompleted();
                }

                return await this.Retrieve(artworkCacheKey, size, 1);
            }

            this.Log().Info("Fetching online link for artwork {0} - {1}", artist, album);
            Uri artworkLink = await this.artworkFetcher.RetrieveAsync(artist, album);

            if (artworkLink == null)
            {
                return null;
            }

            byte[] imageData;

            using (var client = new HttpClient())
            {
                this.Log().Info("Dowloading artwork data for {0} - {1} from {2}", artist, album, artworkLink);

                try
                {
                    imageData = await client.GetByteArrayAsync(artworkLink);
                }

                catch (WebException ex)
                {
                    throw new ArtworkFetchException(string.Format("Unable to download artwork from {0}", artworkLink), ex);
                }
            }

            artworkCacheKey = await this.Store(imageData);

            await this.cache.InsertObject(lookupKey, artworkCacheKey);

            if (!semaphoreExists)
            {
                semaphore.OnNext(Unit.Default);
                semaphore.OnCompleted();
            }

            return await this.Retrieve(artworkCacheKey, size, 1);
        }

        public async Task<IBitmap> Retrieve(string artworkKey, int size, int priority)
        {
            if (artworkKey == null)
                throw new ArgumentNullException("artworkKey");

            if (priority < 1)
                throw new ArgumentOutOfRangeException("priority", "Priority must be greater than zero");

            string keyWithSize = BlobCacheKeys.GetArtworkKeyWithSize(artworkKey, size);

            // If we don't have the small version of an artwork, resize it, save it and return it.
            // This saves us a bunch of memory at the next startup, because BitmapImage has some
            // kind of memory leak, so the not-resized image hangs around in memory forever.
            var operation = Observable.Defer(() => BlobCache.LocalMachine.LoadImage(keyWithSize) // Try to load the small version
                .Catch(BlobCache.LocalMachine.LoadImage(artworkKey, size, size) // If we can't load the small version, resize the image
                    .Do(async x => await SaveImageToBlobCacheAsync(keyWithSize, x)))); // Then save the resized image into the cache

            IBitmap image = await this.queue.EnqueueObservableOperation(priority + 1, () => operation).ToTask();

            return image;
        }

        /// <summary>
        /// Stores the artwork and returns a key to retrieve the artwork again.
        /// </summary>
        public async Task<string> Store(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            string key = BlobCacheKeys.GetKeyForArtwork(data);

            lock (this.keyCache)
            {
                bool added = this.keyCache.Add(key);

                if (!added)
                {
                    return key;
                }
            }

            this.Log().Info("Adding new artwork {0} to the BlobCache", key);

            await queue.EnqueueObservableOperation(1, () => cache.Insert(key, data));

            this.Log().Debug("Added artwork {0} to the BlobCache", key);

            return key;
        }

        private async Task SaveImageToBlobCacheAsync(string key, IBitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                await bitmap.Save(CompressedBitmapFormat.Jpeg, 1, ms);

                await this.cache.Insert(key, ms.ToArray());
            }
        }
    }
}