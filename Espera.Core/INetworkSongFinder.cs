using System;
using System.Collections.Generic;

namespace Espera.Core
{
    public interface INetworkSongFinder<T> where T : Song
    {
        /// <summary>
        /// Searches songs with the specified search term.
        /// </summary>
        /// <param name="searchTerm">
        /// An optional string to search for. <c>null</c> or empty to get a default list (e.g the
        /// most popular songs).
        /// </param>
        /// <exception cref="NetworkSongFinderException">The search failed.</exception>
        IObservable<IReadOnlyList<T>> GetSongsAsync(string searchTerm = null);
    }
}