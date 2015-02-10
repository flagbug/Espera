using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using ReactiveMarrow;
using ReactiveUI;
using Splat;

namespace Espera.Core
{
    public class MusicBrainzArtworkFetcher : IArtworkFetcher, IEnableLogger
    {
        private const string ArtworkEndpoint = "http://coverartarchive.org/release/{0}/";
        private const string SearchEndpoint = "http://www.musicbrainz.org/ws/2/release/?query=artist:{0}+release:{1}";

        private readonly RateLimitedOperationQueue queue;

        public MusicBrainzArtworkFetcher()
        {
            // The MusicBraint search service allows us to perform onme request per second on
            // average, make sure we don't exceed that.
            this.queue = new RateLimitedOperationQueue(TimeSpan.FromSeconds(1.5), RxApp.TaskpoolScheduler);
        }

        public async Task<Uri> RetrieveAsync(string artist, string album)
        {
            // Replace special character, as MusicBrainz uses Lucene in the backend
            artist = Escape(artist);
            album = Escape(album);

            // Only searches are rate-limited, artwork retrievals are fine
            IReadOnlyList<string> releaseIds = await this.queue.EnqueueOperation(() => GetReleaseIdsAsync(artist, album));

            if (releaseIds == null)
            {
                return null;
            }

            return await GetArtworkLinkAsync(releaseIds);
        }

        /// <summary>
        /// Escapes a lucene query
        /// </summary>
        private static String Escape(String s)
        {
            var sb = new StringBuilder();

            foreach (char c in s)
            {
                // These characters are part of the query syntax and must be escaped
                if (c == '\\' || c == '+' || c == '-' || c == '!' || c == '(' || c == ')' || c == ':'
                    || c == '^' || c == '[' || c == ']' || c == '\"' || c == '{' || c == '}' || c == '~'
                    || c == '*' || c == '?' || c == '/')
                {
                    sb.Append(@"\");
                }

                if (c == '|' || c == '&')
                {
                    sb.Append(@"\\");
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static async Task<IReadOnlyList<string>> GetReleaseIdsAsync(string artist, string album)
        {
            string searchRequestUrl = string.Format(SearchEndpoint, artist, album);
            string searchResponse;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", "Espera/2.0 (http://getespera.com)");

                try
                {
                    searchResponse = await client.GetStringAsync(searchRequestUrl);
                }

                catch (HttpRequestException ex)
                {
                    throw new ArtworkFetchException(string.Format("Error while requesting the release id for artist {0} and album {1}", artist, album), ex);
                }
            }

            XNamespace ns = "http://musicbrainz.org/ns/mmd-2.0#";
            var releases = XDocument.Parse(searchResponse).Descendants(ns + "release");

            XNamespace scoreNs = "http://musicbrainz.org/ns/ext#-2.0";

            List<string> releaseIds = releases.Where(x => (int?)x.Attribute(scoreNs + "score") >= 95)
                .Select(x => x.Attribute("id").Value)
                .ToList();

            return releaseIds;
        }

        private async Task<Uri> GetArtworkLinkAsync(IReadOnlyList<string> releaseIds)
        {
            using (var client = new HttpClient())
            {
                foreach (string releaseId in releaseIds)
                {
                    string artworkRequestUrl = string.Format(ArtworkEndpoint, releaseId);

                    HttpResponseMessage response;

                    try
                    {
                        response = await client.GetAsync(artworkRequestUrl);

                        // The only valid failure status is "Not Found"
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                    }

                    catch (HttpRequestException ex)
                    {
                        string errorInfo = string.Format("Could not download artwork informations for release id {0}", releaseId);

                        // If we can't even get the last artwork, throw
                        if (releaseId == releaseIds.Last())
                        {
                            throw new ArtworkFetchException(errorInfo, ex);
                        }

                        if (releaseIds.Count > 1)
                        {
                            this.Log().Error(errorInfo + ", retrying with next in list.");
                        }

                        continue;
                    }

                    string responseContent = await response.Content.ReadAsStringAsync();
                    JToken artworkUrlToken = JObject.Parse(responseContent).SelectToken("images[0].image");

                    if (artworkUrlToken == null)
                    {
                        continue;
                    }

                    return new Uri(artworkUrlToken.ToObject<string>());
                }
            }

            return null;
        }
    }
}
