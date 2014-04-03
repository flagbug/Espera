using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Espera.Core
{
    /// <summary>
    /// This class contains the used keys for Akavache
    /// </summary>
    public static class BlobCacheKeys
    {
        /// <summary>
        /// This is the key prefix for song artworks. After the hyphe, the MD5 hash of the artwork
        /// is attached.
        /// </summary>
        public const string Artwork = "artwork-";

        /// <summary>
        /// This is the key for the changelog that is shown after the application is updated.
        /// </summary>
        public const string Changelog = "changelog";
    }
}