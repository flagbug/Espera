using Espera.Core;
using Espera.Core.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rareform.Validation;
using System;
using System.Collections.Generic;
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

        private async Task GetLibraryContent(JToken dontCare)
        {
            var content = JObject.FromObject(new
            {
                songs = from s in this.library.Songs
                        select new
                        {
                            album = s.Album,
                            artist = s.Artist,
                            duration = s.Duration,
                            genre = s.Genre,
                            title = s.Title,
                            guid = s.Guid
                        }
            });

            await this.SendMessage(content);
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
                }

                else
                {
                    // Handle error
                }
            }

            else
            {
                // Handle error
            }
        }

        private async Task<JObject> ReceiveMessage()
        {
            var buffer = new byte[42];

            await this.RecieveAsync(buffer);

            string header = Encoding.Unicode.GetString(buffer);

            if (header != "espera-client-message")
                throw new Exception("Holy batman, something went terribly wrong!");

            buffer = new byte[4];
            await this.RecieveAsync(buffer);

            int length = BitConverter.ToInt32(buffer, 0);

            buffer = new byte[length];

            await this.RecieveAsync(buffer);

            string content = Encoding.Unicode.GetString(buffer);

            return JObject.Parse(content);
        }

        private async Task RecieveAsync(byte[] buffer)
        {
            int recieved = 0;

            while (recieved < buffer.Length)
            {
                int bytesRecieved = await this.client.GetStream().ReadAsync(buffer, recieved, buffer.Length - recieved);
                recieved += bytesRecieved;
            }
        }

        private async Task SendMessage(JObject content)
        {
            byte[] contentBytes = Encoding.Unicode.GetBytes(content.ToString(Formatting.None));
            byte[] length = BitConverter.GetBytes(contentBytes.Length); // We have a fixed size of 4 bytes
            byte[] headerBytes = Encoding.Unicode.GetBytes("espera-server-message");

            var message = new byte[headerBytes.Length + length.Length + contentBytes.Length];
            headerBytes.CopyTo(message, 0);
            length.CopyTo(message, headerBytes.Length);
            contentBytes.CopyTo(message, headerBytes.Length + length.Length);

            await client.GetStream().WriteAsync(message, 0, message.Length);
            await client.GetStream().FlushAsync();
        }
    }
}