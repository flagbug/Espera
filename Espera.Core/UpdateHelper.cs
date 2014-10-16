using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Splat;

namespace Espera.Core
{
    public static class UpdateHelper
    {
        public static async Task<bool> CheckForPortableUpdate()
        {
            string json;

            try
            {
                using (var client = new HttpClient())
                {
                    json = await client.GetStringAsync("http://getespera.com/versions/current");
                }
            }

            catch (HttpRequestException ex)
            {
                LogHost.Default.ErrorException("Failed to request current version from the Espera server", ex);
                return false;
            }

            string versionString = JObject.Parse(json)["version"].ToString();
            var version = Version.Parse(versionString);

            return version > AppInfo.Version;
        }
    }
}