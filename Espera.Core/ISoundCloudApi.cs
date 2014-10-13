using Refit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Temporary workaround for a bug in Refit where it doesn't include System.Linq and makes the build fail

namespace Espera.Core
{
    public interface ISoundCloudApi
    {
        [Get("/explore/Popular+Music")]
        Task<ExploreResponse> GetPopularTracks(int limit);

        [Get("/tracks.json")]
        Task<IReadOnlyList<SoundCloudSong>> Search([AliasAs("q")] string searchTerm, [AliasAs("client_id")] string clientId);
    }

    public class ExploreResponse
    {
        public IReadOnlyList<SoundCloudSong> Tracks { get; set; }
    }
}