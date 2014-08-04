using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Espera.Core
{
    public class SoundCloudSongFinder : INetworkSongFinder<SoundCloudSong>
    {
        private const string ClientId = "0367b2f7000481e0d1e0815e70c81379";

        public async Task<IReadOnlyList<SoundCloudSong>> GetSongsAsync(string searchTerm)
        {
            var api = RestService.For<ISoundCloudApi>("http://api.soundcloud.com");

            var songs = (await api.Search(searchTerm, ClientId)).Where(x => x.IsStreamable).ToList();

            foreach (SoundCloudSong song in songs)
            {
                song.StreamUrl = new Uri(song.StreamUrl + "?client_id=" + ClientId);
            }

            return songs;
        }
    }
}