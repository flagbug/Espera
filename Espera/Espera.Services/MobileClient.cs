using Espera.Core;
using Espera.Core.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rareform.Validation;
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
    /// <summary>
    /// Represents one mobile endpoint and handles the interaction.
    /// </summary>
    internal class MobileClient
    {
        private readonly TcpClient client;
        private readonly Library library;
        private readonly Dictionary<string, Func<JToken, Task>> messageActionMap;

        public MobileClient(TcpClient client, Library library)
        {
            if (client == null)
                Throw.ArgumentNullException(() => client);

            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.client = client;
            this.library = library;

            this.messageActionMap = new Dictionary<string, Func<JToken, Task>>
            {
                {"get-library-content", this.GetLibraryContent},
                {"post-playlist-song", this.PostPlaylistSong}
            };
        }

        public async Task StartAsync()
        {
            using (client)
            {
                while (true)
                {
                    JObject request = await this.ReceiveMessage();

                    string requestAction = request["action"].ToString();

                    Func<JToken, Task> action;

                    if (this.messageActionMap.TryGetValue(requestAction, out action))
                    {
                        await action(request["parameters"]);
                    }
                }
            }
        }

        private static async Task<byte[]> CompressContentAsync(byte[] content)
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

        private async Task GetLibraryContent(JToken dontCare)
        {
            var content = JObject.FromObject(new
            {
                songs = from s in this.library.Songs
                        select new
                        {
                            album = s.Album,
                            artist = s.Artist,
                            duration = s.Duration.TotalSeconds,
                            genre = s.Genre,
                            title = s.Title,
                            guid = s.Guid
                        }
            });

            await this.SendResponse(200, "Ok", content);
        }

        private async Task PostPlaylistSong(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["songGuid"].ToString(), out songGuid);

            if (valid)
            {
                LocalSong song = this.library.Songs.FirstOrDefault(x => x.Guid == songGuid);

                if (song != null)
                {
                    this.library.AddSongToPlaylist(song);
                    await this.SendResponse(200, "Song added to playlist");
                }

                else
                {
                    await this.SendResponse(404, "Song not found");
                }
            }

            else
            {
                await this.SendResponse(400, "Invalid GUID");
            }
        }

        private async Task ReceiveAsync(byte[] buffer)
        {
            int received = 0;

            while (received < buffer.Length)
            {
                int bytesRecieved = await this.client.GetStream().ReadAsync(buffer, received, buffer.Length - received);
                received += bytesRecieved;
            }
        }

        private async Task<JObject> ReceiveMessage()
        {
            var buffer = new byte[42];

            await this.ReceiveAsync(buffer);

            string header = Encoding.Unicode.GetString(buffer);

            if (header != "espera-client-message")
                throw new Exception("Holy batman, something went terribly wrong!");

            buffer = new byte[4];
            await this.ReceiveAsync(buffer);

            int length = BitConverter.ToInt32(buffer, 0);

            buffer = new byte[length];

            await this.ReceiveAsync(buffer);

            string content = Encoding.Unicode.GetString(buffer);

            return JObject.Parse(content);
        }

        private async Task SendMessage(JObject content)
        {
            byte[] contentBytes = Encoding.Unicode.GetBytes(content.ToString(Formatting.None));

            contentBytes = await CompressContentAsync(contentBytes);

            byte[] length = BitConverter.GetBytes(contentBytes.Length); // We have a fixed size of 4 bytes
            byte[] headerBytes = Encoding.Unicode.GetBytes("espera-server-message");

            var message = new byte[headerBytes.Length + length.Length + contentBytes.Length];
            headerBytes.CopyTo(message, 0);
            length.CopyTo(message, headerBytes.Length);
            contentBytes.CopyTo(message, headerBytes.Length + length.Length);

            await client.GetStream().WriteAsync(message, 0, message.Length);
            await client.GetStream().FlushAsync();
        }

        private async Task SendResponse(int status, string message, JToken content = null)
        {
            var response = new JObject
            {
                {"status", status},
                {"message", message},
            };

            if (content != null)
            {
                response.Add("content", content);
            }

            await this.SendMessage(response);
        }
    }
}