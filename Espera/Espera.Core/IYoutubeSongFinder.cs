using System.Collections.Generic;
using System.Threading.Tasks;

namespace Espera.Core
{
    public interface IYoutubeSongFinder
    {
        Task<IReadOnlyList<YoutubeSong>> GetSongsAsync(string searchTerm);
    }
}