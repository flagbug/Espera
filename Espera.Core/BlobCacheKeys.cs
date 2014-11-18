using System;
using System.Security.Cryptography;

namespace Espera.Core
{
    /// <summary>
    /// This class contains the used keys for Akavache
    /// </summary>
    public static class BlobCacheKeys
    {
        /// <summary>
        /// This is the key prefix for song artworks. After the hyphen, the MD5 hash of the artwork
        /// is attached.
        /// </summary>
        public const string Artwork = "artwork-";

        /// <summary>
        /// This is the key for the changelog that is shown after the application is updated.
        /// </summary>
        public const string Changelog = "changelog";

        public const string OnlineArtwork = "artwork-online-lookup-";

        /// <summary>
        /// Contract for Splat to locate the request cache.
        /// </summary>
        public const string RequestCacheContract = "requestCache";

        public const string SoundCloudPrefix = "soundcloud-search-";

        public const string YoutubePrefix = "youtube-search-";

        /// <summary>
        /// Gets the artwork key for the specified artwork hash and size.
        /// </summary>
        public static string GetArtworkKeyWithSize(string key, int size) => "\{key}-\{size}x\{size}";

        public static string GetKeyForArtwork(byte[] artworkData)
        {
            byte[] hash;

            using (var hashAlgorithm = MD5.Create())
            {
                hash = hashAlgorithm.ComputeHash(artworkData);
            }

            return BlobCacheKeys.Artwork + BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public static string GetKeyForOnlineArtwork(string artist, string album)
        {
            return OnlineArtwork + "\{artist.ToLowerInvariant()}-\{album.ToLowerInvariant()}";
        }

        public static string GetKeyForSoundCloudCache(string searchTerm)
        {
            return SoundCloudPrefix + searchTerm.ToLowerInvariant();
        }

        public static string GetKeyForYoutubeCache(string searchTerm)
        {
            return YoutubePrefix + searchTerm.ToLowerInvariant();
        }
    }
}