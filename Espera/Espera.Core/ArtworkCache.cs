using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Akavache;
using Punchclock;
using ReactiveUI;
using Splat;

namespace Espera.Core
{
    /// <summary>
    /// A smart cache for artworks that prioritizes the retrieving of artworks as well as
    /// prioritizing artwork from artists that come early in the alphabet.
    /// </summary>
    public class ArtworkCache : IEnableLogger
    {
        private static readonly Lazy<ArtworkCache> instance;
        private readonly IBlobCache cache;
        private readonly HashSet<string> keyCache;
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
        }

        public static ArtworkCache Instance
        {
            get { return instance.Value; }
        }

        public Task<IBitmap> Retrieve(string artworkKey, int size, int priority)
        {
            if (artworkKey == null)
                throw new ArgumentNullException("artworkKey");

            if (priority < 1)
                throw new ArgumentOutOfRangeException("priority", "Priority must be greater than zero");

            string keyWithSize = GetKeyWithSize(artworkKey, size);

            // If we don't have the small version of an artwork, resize it, save it and return it.
            // This saves us a bunch of memory at the next startup, because BitmapImage has some
            // kind of memory leak, so the not-resized image hangs around in memory forever.
            var operation = Observable.Defer(() => BlobCache.LocalMachine.LoadImage(keyWithSize) // Try to load the small version
                .Catch(BlobCache.LocalMachine.LoadImage(artworkKey, size, size) // If we can't load the small version, resize the image
                    .Do(async x => await SaveImageToBlobCacheAsync(keyWithSize, x)))); // Then save the resized image into the cache

            return this.queue.EnqueueObservableOperation(priority + 1, () => operation).ToTask();
        }

        /// <summary>
        /// Stores the artwork and returns a key to retrieve the artwork again.
        /// </summary>
        public async Task<string> Store(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            string key = GetKeyForArtwork(data);

            lock (this.keyCache)
            {
                bool added = this.keyCache.Add(key);

                if (!added)
                {
                    return key;
                }
            }

            this.Log().Info("Adding new artwork {0} of to the BlobCache", key);

            await queue.EnqueueObservableOperation(1, () => cache.Insert(key, data));

            this.Log().Debug("Added artwork {0} to the BlobCache", key);

            return key;
        }

        private static string GetKeyForArtwork(byte[] artworkData)
        {
            byte[] hash = MD5.Create().ComputeHash(artworkData);

            return BlobCacheKeys.Artwork + BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private static string GetKeyWithSize(string key, int size)
        {
            return string.Format("{0}-{1}x{1}", key, size);
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