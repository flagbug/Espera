using Espera.Core;
using Espera.Core.Management;
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

        public static JObject SerializePlaylist(Playlist playlist)
        {
            return JObject.FromObject(new
            {
                name = playlist.Name,
                current = playlist.CurrentSongIndex.Value.ToString(),
                songs = playlist.Select(x => new
                {
                    artist = x.Song.Artist,
                    title = x.Song.Title,
                    source = x.Song is LocalSong ? "local" : "youtube",
                    guid = x.Guid
                })
            });
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