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

        public const string SoundCloudPrefix = "soundcloud-search-";

        public const string YoutubePrefix = "youtube-search-";

        public static string GetKeyForYoutubeCache(string searchTerm)
        {
            return YoutubePrefix + searchTerm.ToLowerInvariant();
        }

        /// <summary>
        /// Gets the artwork key for the specified artwork hash and size.
        /// </summary>
        public static string GetArtworkKeyWithSize(string key, int size)
        {
            return String.Format("{0}-{1}x{1}", key, size);
        }

        public static string GetKeyForSoundCloudCache(string searchTerm)
        {
            return SoundCloudPrefix + searchTerm.ToLowerInvariant();
        }

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
            return string.Format(OnlineArtwork + "{0}-{1}", artist.ToLowerInvariant(), album.ToLowerInvariant());
        }
    }
}