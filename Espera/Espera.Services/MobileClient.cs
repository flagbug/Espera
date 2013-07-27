using Espera.Core;
using Espera.Core.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rareform.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Espera.Services
{
    internal class MobileClient
    {
        private readonly TcpClient client;
        private readonly Library library;
        private readonly Dictionary<string, Func<Task>> messageActionMap;

        public MobileClient(TcpClient client, Library library)
        {
            if (client == null)
                Throw.ArgumentNullException(() => client);

            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.client = client;
            this.library = library;

            this.messageActionMap = new Dictionary<string, Func<Task>>
            {
                {"get-library-content", this.SendSongsAsync}
            };
        }

        public async Task StartAsync()
        {
            using (client)
            {
                using (var reader = new StreamReader(client.GetStream(), Encoding.Unicode))
                {
                    while (true)
                    {
                        string line = await reader.ReadLineAsync();

                        Func<Task> action;

                        if (this.messageActionMap.TryGetValue(line, out action))
                        {
                            await action();
                        }
                    }
                }
            }
        }

        private async Task SendMessage(string content)
        {
            byte[] contentBytes = Encoding.Unicode.GetBytes(content);
            byte[] length = BitConverter.GetBytes(contentBytes.Length); // We have a fixed size of 4 bytes
            byte[] headerBytes = Encoding.Unicode.GetBytes("espera-server-message");

            var message = new byte[headerBytes.Length + length.Length + contentBytes.Length];
            headerBytes.CopyTo(message, 0);
            length.CopyTo(message, headerBytes.Length);
            contentBytes.CopyTo(message, headerBytes.Length + length.Length);

            await client.GetStream().WriteAsync(message, 0, message.Length);
            await client.GetStream().FlushAsync();
        }

        private async Task SendSongsAsync()
        {
            var jSongs = JObject.FromObject(new
            {
                action = "library-content",
                songs = from s in this.library.Songs.Cast<LocalSong>()
                        select new
                        {
                            album = s.Album,
                            artist = s.Artist,
                            duration = s.Duration,
                            genre = s.Genre,
                            title = s.Title
                        }
            });

            string json = jSongs.ToString(Formatting.None);

            await this.SendMessage(json);
        }
    }
}