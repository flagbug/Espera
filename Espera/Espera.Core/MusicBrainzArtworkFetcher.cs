using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        private const string SearchEndpoint = "http://www.musicbrainz.org/ws/2/release/?query=artist:{0}+release:{1}";
        private readonly RateLimitedOperationQueue queue;

        public MusicBrainzArtworkFetcher()
        {
            this.queue = new RateLimitedOperationQueue(TimeSpan.FromSeconds(1), RxApp.TaskpoolScheduler);
        }

        public async Task<Uri> RetrieveAsync(string artist, string album)
        {
            // Only searches are rate-limited, artwork retrievals are fine
            string releaseId = await this.queue.EnqueueOperation(() => GetReleaseIdAsync(artist, album));

            if (releaseId == null)
            {
                return null;
            }

            return await GetArtworkLinkAsync(releaseId);
        }

        private static async Task<Uri> GetArtworkLinkAsync(string releaseId)
        {
            using (var client = new HttpClient())
            {
                string artworkRequestUrl = string.Format(ArtworkEndpoint, releaseId);

                string response;

                try
                {
                    response = await client.GetStringAsync(artworkRequestUrl);
                }

                catch (WebException ex)
                {
                    throw new ArtworkFetchException(string.Format("Could not download artwork informations for release id {0}", releaseId), ex);
                }

                JToken artworkUrlToken = JObject.Parse(response).SelectToken("images[0].image");

                if (artworkUrlToken == null)
                {
                    return null;
                }

                return new Uri(artworkUrlToken.ToObject<string>());
            }
        }

        private static async Task<string> GetReleaseIdAsync(string artist, string album)
        {
            using (var client = new HttpClient())
            {
                string searchRequestUrl = string.Format(SearchEndpoint, artist, album);

                string searchResponse;

                try
                {
                    searchResponse = await client.GetStringAsync(searchRequestUrl);
                }

                catch (WebException ex)
                {
                    throw new ArtworkFetchException(string.Format("Error while requesting the release id for artist {0} and album {1}", artist, album), ex);
                }

                XNamespace ns = "http://musicbrainz.org/ns/mmd-2.0#";
                var releases = XDocument.Parse(searchResponse).Descendants(ns + "release");

                IEnumerable<string> releaseIds = releases.Select(x => x.Attribute("id").Value);

                return releaseIds.FirstOrDefault();
            }
        }
    }
}