using System.Collections.Generic;
using System.Threading.Tasks;

namespace Espera.Core
{
    public interface INetworkSongFinder<T> where T : Song
    {
        Task<IReadOnlyList<T>> GetSongsAsync(string searchTerm);
    }
}