using Espera.Core;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Espera.Services
{
    public static class MobileHelper
    {
        public static async Task<byte[]> CompressContentAsync(byte[] content)
        {
            using (var targetStream = new MemoryStream())
            {
                using (var stream = new GZipStream(targetStream, CompressionMode.Compress))
                {
                    await stream.WriteAsync(content, 0, content.Length);
                }

                return targetStream.ToArray();
            }
        }

        public static JObject SerializeSongs(IEnumerable<LocalSong> songs)
        {
            return JObject.FromObject(new
            {
                songs = songs
                    .Select(s => new
                    {
                        album = s.Album,
                        artist = s.Artist,
                        duration = s.Duration.TotalSeconds,
                        genre = s.Genre,
                        title = s.Title,
                        guid = s.Guid
                    })
            });
        }
    }
}