using Akavache;
using Punchclock;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace Espera.Core
{
    /// <summary>
    /// A smart cache for artworks that prioritizes the retrieving of artworks as well as
    /// prioritizing the retrieval of artwork with a priority number.
    /// </summary>
    public class ArtworkCache : IEnableLogger
    {
        /// <summary>
        /// Used to mark online artwork lookups as failed, so we don't try to retrieve it again for
        /// some time.
        /// </summary>
        public static readonly string OnlineFailMark = "FAILED";

        private static readonly Lazy<ArtworkCache> instance;
        private readonly IArtworkFetcher artworkFetcher;
        private readonly IBlobCache cache;
        private readonly KeyedMemoizingSemaphore keyedMemoizingSemaphore;
        private readonly OperationQueue queue;
        private readonly KeyedMemoizingSemaphore storageSemaphore;

        static ArtworkCache()
        {
            instance = new Lazy<ArtworkCache>(() => new ArtworkCache());
        }

        public ArtworkCache(IBlobCache cache = null, IArtworkFetcher artworkFetcher = null)
        {
            this.cache = cache ?? BlobCache.LocalMachine;
            this.artworkFetcher = artworkFetcher ?? new MusicBrainzArtworkFetcher();

            this.queue = new OperationQueue(1); // Disk operations should be serialized
            this.storageSemaphore = new KeyedMemoizingSemaphore();
            this.keyedMemoizingSemaphore = new KeyedMemoizingSemaphore();
        }

        public static ArtworkCache Instance
        {
            get { return instance.Value; }
        }

        /// <summary>
        /// Fetches an artwork for the specified combination of artist and album.
        /// </summary>
        /// <returns>
        /// The cache key of the fetched artwork, or <c>null</c> , if no artwork could be found.
        /// </returns>
        /// <exception cref="ArtworkCacheException">
        /// An error occured while fetching or storing the artwork.
        /// </exception>
        public async Task<string> FetchOnline(string artist, string album)
        {
            if (artist == null)
                throw new ArgumentNullException("artist");

            if (album == null)
                throw new ArgumentNullException("album");

            string lookupKey = BlobCacheKeys.GetKeyForOnlineArtwork(artist, album);

            // Requests with the same lookup key have to wait on the first and then get the cached
            // artwork key. That won't happen often, but when it does, we are save.
            await this.keyedMemoizingSemaphore.Wait(lookupKey);

            string artworkCacheKey = null;

            // Each lookup key gets an artwork key assigned, let's see if it's already in the cache
            if (await this.cache.GetObjectCreatedAt<string>(lookupKey) != null)
            {
                artworkCacheKey = await this.queue.EnqueueObservableOperation(1, () => this.cache.GetObject<string>(lookupKey));
            }

            // Previously failed lookups are marked as failed, it doesn't make sense to let it fail again
            if (artworkCacheKey == OnlineFailMark)
            {
                this.Log().Debug("Key {0} is marked as failed, returning.", lookupKey);

                this.keyedMemoizingSemaphore.Release(lookupKey);

                return null;
            }

            if (artworkCacheKey != null)
            {
                // We already have the artwork cached? Great!

                this.keyedMemoizingSemaphore.Release(lookupKey);

                return artworkCacheKey;
            }

            this.Log().Info("Fetching online link for artwork {0} - {1}", artist, album);

            Uri artworkLink;

            try
            {
                artworkLink = await this.artworkFetcher.RetrieveAsync(artist, album);
            }

            catch (ArtworkFetchException ex)
            {
                this.keyedMemoizingSemaphore.Release(lookupKey);

                throw new ArtworkCacheException("Could not retrieve the artwork information", ex);
            }

            if (artworkLink == null)
            {
                await this.MarkOnlineLookupKeyAsFailed(lookupKey);

                this.keyedMemoizingSemaphore.Release(lookupKey);

                return null;
            }

            byte[] imageData;

            using (var client = new HttpClient())
            {
                this.Log().Info("Downloading artwork data for {0} - {1} from {2}", artist, album, artworkLink);

                try
                {
                    imageData = await client.GetByteArrayAsync(artworkLink);
                }

                catch (HttpRequestException ex)
                {
                    this.keyedMemoizingSemaphore.Release(lookupKey);

                    throw new ArtworkCacheException(String.Format("Unable to download artwork from {0}", artworkLink), ex);
                }
            }

            artworkCacheKey = BlobCacheKeys.GetKeyForArtwork(imageData);
            await this.Store(artworkCacheKey, imageData);

            await this.queue.EnqueueObservableOperation(1, () => this.cache.InsertObject(lookupKey, artworkCacheKey));

            this.keyedMemoizingSemaphore.Release(lookupKey);

            return artworkCacheKey;
        }

        /// <summary>
        /// Retrieves the artwork with the specified key and size and priority from the cache. The
        /// priority can be used to fevor the retrieval of this artwork before others.
        /// </summary>
        /// <exception cref="ArtworkCacheException">An error occured while loading the artwork.</exception>
        public Task<IBitmap> Retrieve(string artworkKey, int size, int priority)
        {
            if (artworkKey == null)
                throw new ArgumentNullException("artworkKey");

            if (size < 0)
                throw new ArgumentOutOfRangeException("size", "Size must be greater than zero");

            if (priority < 1)
                throw new ArgumentOutOfRangeException("priority", "Priority must be greater than zero");

            this.Log().Debug("Requesting artwork with key {0} and size {1} from the cache", artworkKey, size);

            return this.queue.Enqueue(priority + 1, () => this.LoadImageFromCache(artworkKey, size));
        }

        /// <summary>
        /// Stores the artwork and returns a key to retrieve the artwork again.
        /// </summary>
        public async Task Store(string key, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            await this.storageSemaphore.Wait(key);

            if (await this.cache.GetCreatedAt(key) != null)
            {
                this.storageSemaphore.Release(key);
                return;
            }

            this.Log().Debug("Adding new artwork {0} to the BlobCache", key);

            try
            {
                await queue.EnqueueObservableOperation(1, () => cache.Insert(key, data));

                this.Log().Debug("Added artwork {0} to the BlobCache", key);
            }

            finally
            {
                this.storageSemaphore.Release(key);
            }
        }

        private Task<IBitmap> LoadImageFromCache(string key, int size)
        {
            // If we don't have the small version of an artwork, resize it, save it and return it.
            // This saves us a bunch of memory at the next startup, because BitmapImage has some
            // kind of memory leak, so the not-resized image hangs around in memory forever.

            string keyWithSize = BlobCacheKeys.GetArtworkKeyWithSize(key, size);

            return this.LoadImageFromCacheSave(keyWithSize)
                .Catch<IBitmap, KeyNotFoundException>(ex =>
                    this.LoadImageFromCacheSave(key, size)
                    .SelectMany(async resized =>
                    {
                        await this.SaveImageToBlobCacheAsync(key, resized);
                        return resized;
                    }))
                .ToTask();
        }

        private IObservable<IBitmap> LoadImageFromCacheSave(string key, int? size = null)
        {
            return this.cache.LoadImage(key, size, size)
               .Catch<IBitmap, NotSupportedException>(ex =>
                   Observable.Throw<IBitmap>(new ArtworkCacheException("Couldn't load artwork", ex)));
        }

        private Task MarkOnlineLookupKeyAsFailed(string lookupKey)
        {
            this.Log().Info("Could not fetch artwork, marking key {0} as failed", lookupKey);

            // If we can't retrieve an artwork, mark the lookup key as failed and don't look again
            // for the next 7 days.
            return this.queue.EnqueueObservableOperation(1, () => this.cache.InsertObject(lookupKey, OnlineFailMark, TimeSpan.FromDays(7))).ToTask();
        }

        private async Task SaveImageToBlobCacheAsync(string key, IBitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                try
                {
                    await bitmap.Save(CompressedBitmapFormat.Jpeg, 1, ms);
                }

                catch (NotSupportedException ex)
                {
                    throw new ArtworkCacheException("Couldn't save artwork", ex);
                }

                await this.cache.Insert(key, ms.ToArray());
            }
        }
    }
}