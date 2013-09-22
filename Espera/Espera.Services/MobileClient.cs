using Espera.Core;
using Espera.Core.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rareform.Validation;
using ReactiveSockets;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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
        private readonly Library library;
        private readonly Dictionary<string, Func<JToken, Task<JObject>>> messageActionMap;
        private readonly IReactiveSocket socket;

        public MobileClient(IReactiveSocket socket, Library library)
        {
            if (socket == null)
                Throw.ArgumentNullException(() => socket);

            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.socket = socket;
            this.library = library;

            this.messageActionMap = new Dictionary<string, Func<JToken, Task<JObject>>>
            {
                {"get-library-content", this.GetLibraryContent},
                {"post-playlist-song", this.PostPlaylistSong},
                {"post-play-instantly", this.PostPlayInstantly},
                {"get-current-playlist", this.GetCurrentPlaylist},
                {"post-play-playlist-song", this.PostPlayPlaylistSong}
            };
        }

        public async Task ListenAsync(CancellationTokenSource token)
        {
            IObservable<JObject> messages =
                socket.Receiver.Buffer(4)
                    .Select(length => BitConverter.ToInt32(length.ToArray(), 0))
                    .Select(length => this.socket.Receiver.Take(length).ToEnumerable().ToArray())
                    .SelectMany(body => MobileHelper.DecompressDataAsync(body).ToObservable())
                    .Select(body => Encoding.Unicode.GetString(body))
                    .Select(JObject.Parse);

            await messages.ObserveOn(RxApp.MainThreadScheduler).ForEachAsync(async request =>
            {
                string requestAction = request["action"].ToString();

                Func<JToken, Task<JObject>> action;

                if (this.messageActionMap.TryGetValue(requestAction, out action))
                {
                    try
                    {
                        JObject response = await action(request["parameters"]);

                        response.Add("id", request["id"]);

                        await this.SendMessage(response);
                    }

                    catch (Exception)
                    {
                        if (Debugger.IsAttached)
                        {
                            Debugger.Break();
                        }

                        // Don't crash the listener if we receive a bogus message that we can't handle
                    }
                }
            });
        }

        private static JObject CreateResponse(int status, string message, JToken content = null)
        {
            var response = new JObject
            {
                {"status", status},
                {"message", message},
                {"type", "response"},
            };

            if (content != null)
            {
                response.Add("content", content);
            }

            return response;
        }

        private Task<JObject> GetCurrentPlaylist(JToken dontCare)
        {
            Playlist playlist = this.library.CurrentPlaylist;

            JObject content = MobileHelper.SerializePlaylist(playlist);

            return Task.FromResult(CreateResponse(200, "Ok", content));
        }

        private Task<JObject> GetLibraryContent(JToken dontCare)
        {
            JObject content = MobileHelper.SerializeSongs(this.library.Songs);

            return Task.FromResult(CreateResponse(200, "Ok", content));
        }

        private async Task<JObject> PostPlayInstantly(JToken parameters)
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
                    return CreateResponse(400, "One or more GUIDs are malformed");
                }
            }

            Dictionary<Guid, LocalSong> dic = this.library.Songs.ToDictionary(x => x.Guid);

            List<LocalSong> songs = guids.Select(x =>
            {
                LocalSong song;

                dic.TryGetValue(x, out song);

                return song;
            })
            .Where(x => x != null)
            .ToList();

            if (guids.Count == songs.Count)
            {
                await this.library.PlayInstantlyAsync(songs);
                return CreateResponse(200, "Ok");
            }

            return CreateResponse(404, "One or more songs could not be found");
        }

        private Task<JObject> PostPlaylistSong(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["songGuid"].ToString(), out songGuid);

            if (valid)
            {
                LocalSong song = this.library.Songs.FirstOrDefault(x => x.Guid == songGuid);

                if (song != null)
                {
                    this.library.AddSongToPlaylist(song);
                    return Task.FromResult(CreateResponse(200, "Song added to playlist"));
                }

                return Task.FromResult(CreateResponse(404, "Song not found"));
            }

            return Task.FromResult(CreateResponse(400, "Malformed GUID"));
        }

        private async Task<JObject> PostPlayPlaylistSong(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (valid)
            {
                PlaylistEntry entry = this.library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

                if (entry != null)
                {
                    await this.library.PlaySongAsync(entry.Index);
                    return CreateResponse(200, "Playing song");
                }

                return CreateResponse(404, "Playlist entry not found");
            }

            return CreateResponse(400, "Malformed GUID");
        }

        private async Task SendMessage(JObject content)
        {
            byte[] contentBytes = Encoding.Unicode.GetBytes(content.ToString(Formatting.None));

            contentBytes = await MobileHelper.CompressDataAsync(contentBytes);

            byte[] length = BitConverter.GetBytes(contentBytes.Length); // We have a fixed size of 4 bytes

            byte[] message = length.Concat(contentBytes).ToArray();

            await this.socket.SendAsync(message);
        }
    }
}