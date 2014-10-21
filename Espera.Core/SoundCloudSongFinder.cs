using System.Reactive.Linq;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using Akavache;

namespace Espera.Core
{
    public class SoundCloudSongFinder : INetworkSongFinder<SoundCloudSong>
    {
        /// <summary>
        /// The time a search with a given search term is cached.
        /// </summary>
        public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(60);

        private const string ClientId = "0367b2f7000481e0d1e0815e70c81379";
        private readonly IBlobCache requestCache;

        /// <summary>
        /// Creates a new instance of the <see cref="SoundCloudSongFinder" /> class.
        /// </summary>
        /// <param name="requestCache">
        /// A <see cref="IBlobCache" /> to cache the search requests. Requests with the same search
        /// term are considered the same.
        /// </param>
        public SoundCloudSongFinder(IBlobCache requestCache)
        {
            if (requestCache == null)
                throw new ArgumentNullException("requestCache");

            this.requestCache = requestCache;
        }

        public IObservable<IReadOnlyList<SoundCloudSong>> GetSongsAsync(string searchTerm = null)
        {
            searchTerm = searchTerm ?? string.Empty;

            IObservable<IReadOnlyList<SoundCloudSong>> retrievalFunc = Observable.Defer(() =>
                requestCache.GetOrFetchObject(BlobCacheKeys.GetKeyForSoundCloudCache(searchTerm), () =>
                    string.IsNullOrWhiteSpace(searchTerm) ? GetPopularSongs() : SearchSongs(searchTerm), DateTimeOffset.Now + CacheDuration));

            return retrievalFunc.Catch<IReadOnlyList<SoundCloudSong>, Exception>(ex =>
                    Observable.Throw<IReadOnlyList<SoundCloudSong>>(new NetworkSongFinderException("SoundCloud search failed", ex)))
                .Select(x => x.Where(y => y.IsStreamable || y.IsDownloadable).ToList())
                .Do(SetupSongUrls);
        }

        private static IObservable<IReadOnlyList<SoundCloudSong>> GetPopularSongs()
        {
            return RestService.For<ISoundCloudApi>("http://api-v2.soundcloud.com").GetPopularTracks(50).Select(x => x.Tracks);
        }

        private static IObservable<IReadOnlyList<SoundCloudSong>> SearchSongs(string searchTerm)
        {
            return RestService.For<ISoundCloudApi>("http://api.soundcloud.com").Search(searchTerm, ClientId);
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