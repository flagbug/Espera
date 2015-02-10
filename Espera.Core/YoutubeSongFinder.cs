using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Akavache;
using Google.GData.Client;
using Google.GData.YouTube;
using Google.YouTube;

namespace Espera.Core
{
    public sealed class YoutubeSongFinder : IYoutubeSongFinder
    {
        /// <summary>
        /// The time a search with a given search term is cached.
        /// </summary>
        public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(60);

        private const string ApiKey =
            "AI39si5_zcffmO_ErRSZ9xUkfy_XxPZLWuxTOzI_1RH9HhXDI-GaaQ-j6MONkl2JiF01yBDgBFPbC8-mn6U9Qo4Ek50nKcqH5g";

        private const int RequestLimit = 50;

        private readonly IBlobCache requestCache;

        /// <summary>
        /// Creates a new instance of the <see cref="YoutubeSongFinder" /> class.
        /// </summary>
        /// <param name="requestCache">
        /// A <see cref="IBlobCache" /> to cache the search requests. Requests with the same search
        /// term are considered the same.
        /// </param>
        public YoutubeSongFinder(IBlobCache requestCache)
        {
            if (requestCache == null)
                throw new ArgumentNullException("requestCache");

            this.requestCache = requestCache;
        }

        public IObservable<IReadOnlyList<YoutubeSong>> GetSongsAsync(string searchTerm = null)
        {
            searchTerm = searchTerm ?? string.Empty;

            return Observable.Defer(() => requestCache.GetOrFetchObject(BlobCacheKeys.GetKeyForYoutubeCache(searchTerm),
                () => RealSearch(searchTerm), DateTimeOffset.Now + CacheDuration));
        }

        public async Task<YoutubeSong> ResolveYoutubeSongFromUrl(Uri url)
        {
            return (await GetSongsAsync(url.OriginalString)).FirstOrDefault();
        }

        private static IObservable<IReadOnlyList<YoutubeSong>> RealSearch(string searchTerm)
        {
            var query = new YouTubeQuery(YouTubeQuery.DefaultVideoUri)
            {
                OrderBy = "relevance",
                Query = searchTerm,
                SafeSearch = YouTubeQuery.SafeSearchValues.None,
                NumberToRetrieve = RequestLimit
            };

            // NB: I have no idea where this API blocks exactly
            var settings = new YouTubeRequestSettings("Espera", ApiKey);
            var request = new YouTubeRequest(settings);

            return Observable.FromAsync(async () =>
            {
                Feed<Video> feed = await Task.Run(() => request.Get<Video>(query));
                List<Video> entries = await Task.Run(() => feed.Entries.ToList());

                var songs = new List<YoutubeSong>();

                foreach (Video video in entries)
                {
                    var duration = TimeSpan.FromSeconds(Int32.Parse(video.YouTubeEntry.Duration.Seconds));
                    string url = video.WatchPage.OriginalString
                        .Replace("&feature=youtube_gdata_player", String.Empty) // Unnecessary long url
                        .Replace("https://", "http://"); // Secure connections are not always easy to handle when streaming

                    var song = new YoutubeSong(url, duration)
                    {
                        Artist = video.Uploader,
                        Title = video.Title,
                        Description = video.Description,
                        Rating = video.RatingAverage >= 1 ? video.RatingAverage : (double?)null,
                        ThumbnailSource = new Uri(video.Thumbnails[0].Url),
                        Views = video.ViewCount
                    };

                    songs.Add(song);
                }

                return songs;
            })
                // The API gives no clue what can throw, wrap it all up
            .Catch<IReadOnlyList<YoutubeSong>, Exception>(ex => Observable.Throw<IReadOnlyList<YoutubeSong>>(new NetworkSongFinderException("YoutubeSongFinder search failed", ex)));
        }
    }
}