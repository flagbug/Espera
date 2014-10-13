using System;
using System.Collections.Generic;

namespace Espera.Core
{
    public interface INetworkSongFinder<T> where T : Song
    {
        /// <summary>
        /// Searches songs with the specified search term.
        /// </summary>
        /// <exception cref="NetworkSongFinderException">The search failed.</exception>
        IObservable<IReadOnlyList<T>> GetSongsAsync(string searchTerm);
    }
}