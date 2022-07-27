using System;
using System.Threading.Tasks;

namespace Espera.Core
{
    public interface IYoutubeSongFinder : INetworkSongFinder<YoutubeSong>
    {
        Task<YoutubeSong> ResolveYoutubeSongFromUrl(Uri url);
    }
}