using Espera.Core;
using Espera.Core.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Espera.Services
{
    public static class MobileHelper
    {
        public static async Task<byte[]> CompressDataAsync(byte[] data)
        {
            using (var targetStream = new MemoryStream())
            {
                using (var stream = new GZipStream(targetStream, CompressionMode.Compress))
                {
                    await stream.WriteAsync(data, 0, data.Length);
                }

                return targetStream.ToArray();
            }
        }

        public static async Task<byte[]> DecompressDataAsync(byte[] data)
        {
            using (var sourceStream = new MemoryStream(data))
            {
                using (var stream = new GZipStream(sourceStream, CompressionMode.Decompress))
                {
                    using (var targetStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(targetStream);
                        return targetStream.ToArray();
                    }
                }
            }
        }

        public static async Task<byte[]> PackMessage(JObject message)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(message.ToString(Formatting.None));

            contentBytes = await CompressDataAsync(contentBytes);

            byte[] length = BitConverter.GetBytes(contentBytes.Length); // We have a fixed size of 4 bytes

            return length.Concat(contentBytes).ToArray();
        }

        /// <summary>
        /// Reads the next message for the Espera protocol from the TCP client.
        /// </summary>
        /// <returns>The uncompressed, deserialized message in JSON, or null, if the underlying client has closed the connection.</returns>
        public static async Task<JObject> ReadNextMessage(this TcpClient client)
        {
            byte[] messageLength = await client.ReadAsync(4);

            if (messageLength.Length == 0)
            {
                return null;
            }

            int realMessageLength = BitConverter.ToInt32(messageLength, 0);

            byte[] messageContent = await client.ReadAsync(realMessageLength);

            if (messageLength.Length == 0)
            {
                return null;
            }

            byte[] decompressed = await DecompressDataAsync(messageContent);
            string decoded = Encoding.UTF8.GetString(decompressed);

            JObject jsonMessage = JObject.Parse(decoded);

            return jsonMessage;
        }

        public static JObject SerializePlaylist(Playlist playlist, int remainingVotes)
        {
            return JObject.FromObject(new
            {
                name = playlist.Name,
                current = playlist.CurrentSongIndex.Value,
                songs = playlist.Select(x => new
                {
                    artist = x.Song.Artist,
                    title = x.Song.Title,
                    source = x.Song is LocalSong ? "local" : "youtube",
                    guid = x.Guid
                }),
                remainingVotes
            });
        }

        public static JObject SerializeSongs(IEnumerable<LocalSong> songs)
        {
            return JObject.FromObject(new
            {
                songs = songs.Select(s => new
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

        private static async Task<byte[]> ReadAsync(this TcpClient client, int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException("length", "Length must be greater than 0");

            int count = 0;
            var buffer = new byte[length];

            do
            {
                int read = await client.GetStream().ReadAsync(buffer, count, length - count);
                count += read;

                // The client has closed the connection
                if (read == 0)
                    return new byte[0];
            }
            while (count < length);

            return buffer;
        }
    }
}