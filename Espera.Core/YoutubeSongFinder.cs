using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Xml;
using Akavache;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace Espera.Core
{
    public sealed class YoutubeSongFinder : IYoutubeSongFinder
    {
        /// <summary>
        /// The time a search with a given search term is cached.
        /// </summary>
        public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(60);

        private const string ApiAppName = "Espera";
        private const string ApiKey = "abc123 ";

        private const string YoutubeVideoUrl = "http://www.youtube.com/watch?v=";

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
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Observable.Empty<IReadOnlyList<YoutubeSong>>();
            }

            var youTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = ApiKey,
                ApplicationName = ApiAppName
            });

            var searchListRequest = youTubeService.Search.List("snippet");
            searchListRequest.MaxResults = RequestLimit;
            searchListRequest.Q = searchTerm;
            searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;
            searchListRequest.Type = "video";

            return Observable.FromAsync(async () =>
            {
                var songs = new List<YoutubeSong>();
                var searchListResponse = await searchListRequest.ExecuteAsync();

                var idSnippet = searchListResponse.Items.ToDictionary(i => i.Id.VideoId, i => i);
                var videoListRequest = youTubeService.Videos.List("contentDetails,statistics");
                videoListRequest.Id = string.Join(",", searchListResponse.Items.Select(i => i.Id.VideoId));

                var videoListResponse = await videoListRequest.ExecuteAsync();

                foreach (var video in videoListResponse.Items)
                {
                    var duration = XmlConvert.ToTimeSpan(video.ContentDetails.Duration);
                    var url = YoutubeVideoUrl + video.Id;
                    SearchResult snippet;

                    if (!idSnippet.TryGetValue(video.Id, out snippet))
                    {
                        continue;
                    }

                    var song = new YoutubeSong(url, duration)
                    {
                        Artist = snippet.Snippet.ChannelTitle,
                        Title = snippet.Snippet.Title,
                        Description = snippet.Snippet.Description,
                        ThumbnailSource = new Uri(snippet.Snippet.Thumbnails.Medium.Url),
                        Views = video.Statistics.ViewCount == null ? 0 : (int)video.Statistics.ViewCount
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