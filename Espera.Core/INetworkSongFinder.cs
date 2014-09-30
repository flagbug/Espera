using System.Collections.Generic;
using System.Threading.Tasks;

namespace Espera.Core
{
    public interface INetworkSongFinder<T> where T : Song
    {
        /// <summary>
        /// Searches songs with the specified search term.
        /// </summary>
        /// <exception cref="NetworkSongFinderException">The search failed.</exception>
        Task<IReadOnlyList<T>> GetSongsAsync(string searchTerm);
    }
}