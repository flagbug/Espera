using Google.GData.Client;
using Google.GData.YouTube;
using Google.YouTube;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Espera.Core
{
    public sealed class YoutubeSongFinder : IYoutubeSongFinder
    {
        private const string ApiKey =
            "AI39si5_zcffmO_ErRSZ9xUkfy_XxPZLWuxTOzI_1RH9HhXDI-GaaQ-j6MONkl2JiF01yBDgBFPbC8-mn6U9Qo4Ek50nKcqH5g";

        public async Task<IReadOnlyList<YoutubeSong>> GetSongsAsync(string searchTerm)
        {
            var query = new YouTubeQuery(YouTubeQuery.DefaultVideoUri)
            {
                OrderBy = "relevance",
                Query = searchTerm,
                SafeSearch = YouTubeQuery.SafeSearchValues.None
            };

            // NB: I have no idea where this API blocks exactly
            var settings = new YouTubeRequestSettings("Espera", ApiKey);
            var request = new YouTubeRequest(settings);
            Feed<Video> feed = await Task.Run(() => request.Get<Video>(query));

            var songs = new List<YoutubeSong>();

            foreach (Video video in await Task.Run(() => feed.Entries))
            {
                var duration = TimeSpan.FromSeconds(Int32.Parse(video.YouTubeEntry.Duration.Seconds));
                string url = video.WatchPage.OriginalString
                    .Replace("&feature=youtube_gdata_player", String.Empty) // Unnecessary long url
                    .Replace("https://", "http://"); // Secure connections are not always easy to handle when streaming

                var song = new YoutubeSong(url, duration)
                {
                    Title = video.Title,
                    Description = video.Description,
                    Rating = video.RatingAverage >= 1 ? video.RatingAverage : (double?)null,
                    ThumbnailSource = new Uri(video.Thumbnails[0].Url),
                    Views = video.ViewCount
                };

                songs.Add(song);
            }

            return songs;
        }
    }
}