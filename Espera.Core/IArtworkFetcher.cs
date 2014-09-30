using System;
using System.Threading.Tasks;

namespace Espera.Core
{
    public interface IArtworkFetcher
    {
        /// <summary>
        /// Retrives an artwork for the specified artist and album.
        /// </summary>
        /// <returns>The download URL of the artwork or null, if the artwork couldn't be found.</returns>
        /// <exception cref="ArtworkFetchException">
        /// An exception has occured while fetching the artwork.
        /// </exception>
        Task<Uri> RetrieveAsync(string artist, string album);
    }
}