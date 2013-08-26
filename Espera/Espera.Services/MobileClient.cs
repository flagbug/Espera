using Espera.Core;
using Espera.Core.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rareform.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Services
{
    /// <summary>
    /// Represents one mobile endpoint and handles the interaction.
    /// </summary>
    public class MobileClient
    {
        private readonly IEsperaNetworkClient client;
        private readonly Library library;
        private readonly Dictionary<string, Func<JToken, Task>> messageActionMap;

        public MobileClient(IEsperaNetworkClient client, Library library)
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
                {"post-playlist-song", this.PostPlaylistSong},
                {"post-play-instantly", this.PostPlayInstantly}
            };
        }

        public async Task StartAsync(CancellationTokenSource token)
        {
            using (client)
            {
                while (!token.IsCancellationRequested)
                {
                    JObject request = await this.client.ReceiveMessage();

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
            var content = MobileHelper.SerializeSongs(this.library.Songs);

            await this.SendResponse(200, "Ok", content);
        }

        private async Task PostPlayInstantly(JToken parameters)
        {
            var guids = new List<Guid>();

            foreach (string guidString in parameters["guids"].Select(x => x.ToString()))
            {
                Guid guid;

                bool valid = Guid.TryParse(guidString, out guid);

                if (valid)
                {
                    guids.Add(guid);
                }

                else
                {
                    await this.SendResponse(400, "One or more GUIDs are malformed");
                }
            }

            Dictionary<Guid, LocalSong> dic = this.library.Songs.ToDictionary(x => x.Guid);
            List<LocalSong> songs = guids.Select(x =>
            {
                LocalSong song;

                bool hasSong = dic.TryGetValue(x, out song);

                if (!hasSong)
                {
                    this.SendResponse(404, "One or more songs could not be found");

                    return null;
                }

                return song;
            })
            .TakeWhile(x => x != null)
            .ToList();

            if (guids.Count == songs.Count)
            {
                await this.library.PlayInstantlyAsync(songs);
                await this.SendResponse(200, "Ok");
            }
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
                await this.SendResponse(400, "Malformed GUID");
            }
        }

        private async Task SendMessage(JObject content)
        {
            byte[] contentBytes = Encoding.Unicode.GetBytes(content.ToString(Formatting.None));

            contentBytes = await MobileHelper.CompressContentAsync(contentBytes);

            byte[] length = BitConverter.GetBytes(contentBytes.Length); // We have a fixed size of 4 bytes
            byte[] headerBytes = Encoding.Unicode.GetBytes("espera-server-message");

            var message = new byte[headerBytes.Length + length.Length + contentBytes.Length];
            headerBytes.CopyTo(message, 0);
            length.CopyTo(message, headerBytes.Length);
            contentBytes.CopyTo(message, headerBytes.Length + length.Length);

            await this.client.SendAsync(message);
        }

        private async Task SendResponse(int status, string message, JToken content = null)
        {
            var response = new JObject
            {
                {"status", status},
                {"message", message}
            };

            if (content != null)
            {
                response.Add("content", content);
            }

            await this.SendMessage(response);
        }
    }
}