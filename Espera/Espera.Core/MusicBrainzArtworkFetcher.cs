using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.Core
{
    public class MusicBrainzArtworkFetcher : IArtworkFetcher
    {
        private const string ArtworkEndpoint = "http://coverartarchive.org/release/{0}/";
        private const string SearchEndpoint = "http://www.musicbrainz.org/ws/2/recording/?query=artist:{0}+release:{1}";
        private readonly RateLimitedOperationQueue queue;

        public MusicBrainzArtworkFetcher()
        {
            this.queue = new RateLimitedOperationQueue(TimeSpan.FromSeconds(1), RxApp.TaskpoolScheduler);
        }

        public async Task<Uri> RetrieveAsync(string artist, string album)
        {
            // Only searches are rate-limited, artwork retrievals are fine
            string releaseId = await this.queue.EnqueueOperation(() => GetReleaseIdAsync(artist, album));

            return await GetArtworkLinkAsync(releaseId);
        }

        private static async Task<Uri> GetArtworkLinkAsync(string releaseId)
        {
            using (var client = new HttpClient())
            {
                string artworkRequestUrl = string.Format(ArtworkEndpoint, releaseId);
                string response = await client.GetStringAsync(artworkRequestUrl);

                string artworkUrl = JObject.Parse(response).SelectToken("images[0].image").Value<string>();

                return new Uri(artworkUrl);
            }
        }

        private static async Task<string> GetReleaseIdAsync(string artist, string album)
        {
            using (var client = new HttpClient())
            {
                string searchRequestUrl = string.Format(SearchEndpoint, artist, album);
                string searchResponse = await client.GetStringAsync(searchRequestUrl);

                XNamespace ns = "http://musicbrainz.org/ns/mmd-2.0#";
                var releases = XDocument.Parse(searchResponse).Descendants(ns + "release");

                IEnumerable<string> releaseIds = releases.Select(x => x.Attribute("id").Value);

                return releaseIds.First();
            }
        }
    }
}