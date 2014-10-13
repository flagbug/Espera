using System.Reactive.Linq;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Espera.Core
{
    public class SoundCloudSongFinder : INetworkSongFinder<SoundCloudSong>
    {
        private const string ClientId = "0367b2f7000481e0d1e0815e70c81379";

        public IObservable<IReadOnlyList<SoundCloudSong>> GetSongsAsync(string searchTerm)
        {
            IObservable<IReadOnlyList<SoundCloudSong>> retrievalFunc = string.IsNullOrWhiteSpace(searchTerm) ?
                RestService.For<ISoundCloudApi>("http://api-v2.soundcloud.com").GetPopularTracks(50).Select(x => x.Tracks) :
                RestService.For<ISoundCloudApi>("http://api.soundcloud.com").Search(searchTerm, ClientId);

            return retrievalFunc.Catch<IReadOnlyList<SoundCloudSong>, Exception>(ex =>
                    Observable.Throw<IReadOnlyList<SoundCloudSong>>(new NetworkSongFinderException("SoundCloud search failed", ex)))
                .Select(x => x.Where(y => y.IsStreamable || y.IsDownloadable).ToList())
                .Do(SetupSongUrls);
        }

        private static void SetupSongUrls(IEnumerable<SoundCloudSong> songs)
        {
            foreach (SoundCloudSong song in songs)
            {
                if (song.IsStreamable)
                {
                    song.StreamUrl = new Uri(song.StreamUrl + "?client_id=" + ClientId);
                }

                if (song.IsDownloadable)
                {
                    song.DownloadUrl = new Uri(song.DownloadUrl + "?client_id=" + ClientId);
                }
            }
        }
    }
}