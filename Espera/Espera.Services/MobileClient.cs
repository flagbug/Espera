using Espera.Core;
using Espera.Core.Audio;
using Espera.Core.Management;
using Newtonsoft.Json.Linq;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveSockets;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Espera.Services
{
    /// <summary>
    /// Represents one mobile endpoint and handles the interaction.
    /// </summary>
    public class MobileClient : IDisposable, IEnableLogger
    {
        private readonly Guid accessToken;
        private readonly Subject<Unit> disconnected;
        private readonly CompositeDisposable disposable;
        private readonly SemaphoreSlim gate;
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

            this.accessToken = this.library.RemoteAccessControl.RegisterRemoteAccessToken();

            this.Log().Info("Registering new mobile client with access token {0}", this.accessToken);

            this.disposable = new CompositeDisposable();
            this.gate = new SemaphoreSlim(1, 1);
            this.disconnected = new Subject<Unit>();

            this.messageActionMap = new Dictionary<string, Func<JToken, Task<JObject>>>
            {
                {"get-library-content", this.GetLibraryContent},
                {"post-playlist-song", this.PostPlaylistSong},
                {"post-play-instantly", this.PostPlayInstantly},
                {"get-current-playlist", this.GetCurrentPlaylist},
                {"post-play-playlist-song", this.PostPlayPlaylistSong},
                {"post-continue-song", this.PostContinueSong},
                {"post-pause-song", this.PostPauseSong},
                {"post-play-next-song", this.PostPlayNextSong},
                {"post-play-previous-song", this.PostPlayPreviousSong},
                {"get-playback-state", this.GetPlaybackState},
                {"post-remove-playlist-song", this.PostRemovePlaylistSong},
                {"get-access-permission", this.GetAccessPermission},
                {"post-administrator-password", this.PostAdministratorPassword},
                {"get-server-version", GetServerVersion},
                {"move-playlist-song-up", this.MovePlaylistSongUp},
                {"move-playlist-song-down", this.MovePlaylistSongDown}
            };

            this.Disconnected = Observable.FromEventPattern(h => this.socket.Disconnected += h, h => this.socket.Disconnected -= h)
                .Select(_ => Unit.Default)
                .Merge(this.disconnected)
                .FirstAsync();
        }

        public IObservable<Unit> Disconnected { get; private set; }

        public void Dispose()
        {
            this.socket.Dispose();
            this.gate.Dispose();
            this.disconnected.Dispose();
            this.disposable.Dispose();
        }

        public void ListenAsync()
        {
            IObservable<JObject> messages =
                socket.Receiver.Buffer(4)
                    .Select(length => BitConverter.ToInt32(length.ToArray(), 0))
                    .Select(length => this.socket.Receiver.Take(length).ToEnumerable().ToArray())
                    .SelectMany(body => MobileHelper.DecompressDataAsync(body).ToObservable())
                    .Select(body => Encoding.UTF8.GetString(body))
                    .Select(JObject.Parse);

            messages.ObserveOn(RxApp.MainThreadScheduler).Subscribe(async request =>
            {
                if (request["action"] == null)
                {
                    this.Log().Warn("Mobile client with access token {0} sent a request without specifiying an action!", this.accessToken);
                    return;
                }

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

                    catch (Exception ex)
                    {
                        this.Log().ErrorException(string.Format(
                            "Mobile client with access token {0} sent a request that caused an exception", this.accessToken), ex);
                        if (Debugger.IsAttached)
                        {
                            Debugger.Break();
                        }

                        // Don't crash the listener if we receive a bogus message that we can't handle
                    }
                }
            }).DisposeWith(this.disposable);

            this.library.CurrentPlaylistChanged.Merge(this.library.CurrentPlaylistChanged
                    .Select(x => x.Changed().Select(y => x))
                    .Switch())
                .Subscribe(x => this.PushPlaylist(x))
                .DisposeWith(this.disposable);

            this.library.CurrentPlaylistChanged.StartWith(this.library.CurrentPlaylist)
                .Select(x => x.CurrentSongIndex.Skip(1))
                .Switch()
                .Subscribe(x => this.PushPlaylistIndex(x))
                .DisposeWith(this.disposable);

            this.library.PlaybackState.Skip(1)
                .Subscribe(x => this.PushPlaybackState(x))
                .DisposeWith(this.disposable);

            this.library.RemoteAccessControl.ObserveAccessPermission(this.accessToken)
                .Skip(1)
                .Subscribe(x => this.PushAccessPermission(x))
                .DisposeWith(this.disposable);
        }

        private static JObject CreatePush(string action, JToken content)
        {
            var payload = new JObject
            {
                {"action", action},
                {"type", "push"},
                {"content", content}
            };

            return payload;
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

        private static Task<JObject> GetServerVersion(JToken dontCare)
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;

            var response = new JObject
            {
                {"version", version.ToString()}
            };

            return Task.FromResult(CreateResponse(200, "Ok", response));
        }

        private async Task<JObject> GetAccessPermission(JToken arg)
        {
            AccessPermission permission = await this.library.RemoteAccessControl.ObserveAccessPermission(this.accessToken).FirstAsync();

            var content = new JObject
            {
                {"accessPermission", permission.ToString()}
            };

            return CreateResponse(200, "Ok", content);
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

        private async Task<JObject> GetPlaybackState(JToken dontCare)
        {
            AudioPlayerState state = await this.library.PlaybackState.FirstAsync();

            var content = new JObject
            {
                {"state", state.ToString()}
            };

            return CreateResponse(200, "Ok", content);
        }

        private Task<JObject> MovePlaylistSongDown(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (valid)
            {
                PlaylistEntry entry = this.library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

                if (entry != null)
                {
                    try
                    {
                        this.library.MovePlaylistSongDown(entry.Index, this.accessToken);
                    }

                    catch (AccessException)
                    {
                        return Task.FromResult(CreateResponse(401, "Unauthorized"));
                    }

                    return Task.FromResult(CreateResponse(200, "Moved song down"));
                }

                return Task.FromResult(CreateResponse(404, "Playlist entry not found"));
            }

            return Task.FromResult(CreateResponse(400, "Malformed GUID"));
        }

        private Task<JObject> MovePlaylistSongUp(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (valid)
            {
                PlaylistEntry entry = this.library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

                if (entry != null)
                {
                    try
                    {
                        this.library.MovePlaylistSongUp(entry.Index, this.accessToken);
                    }

                    catch (AccessException)
                    {
                        return Task.FromResult(CreateResponse(401, "Unauthorized"));
                    }

                    return Task.FromResult(CreateResponse(200, "Moved song up"));
                }

                return Task.FromResult(CreateResponse(404, "Playlist entry not found"));
            }

            return Task.FromResult(CreateResponse(400, "Malformed GUID"));
        }

        private Task<JObject> PostAdministratorPassword(JToken parameters)
        {
            string password = parameters["password"].ToString();

            try
            {
                this.library.RemoteAccessControl.UpgradeRemoteAccess(this.accessToken, password);
            }

            catch (WrongPasswordException)
            {
                return Task.FromResult(CreateResponse(401, "Wrong password"));
            }

            return Task.FromResult(CreateResponse(200, "Ok"));
        }

        private async Task<JObject> PostContinueSong(JToken content)
        {
            try
            {
                await this.library.ContinueSongAsync(this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(401, "Unauthorized");
            }

            return CreateResponse(200, "Ok");
        }

        private async Task<JObject> PostPauseSong(JToken dontCare)
        {
            try
            {
                await this.library.PauseSongAsync(this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(401, "Unauthorized");
            }

            return CreateResponse(200, "Ok");
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
                try
                {
                    await this.library.PlayInstantlyAsync(songs, this.accessToken);
                }

                catch (AccessException)
                {
                    return CreateResponse(401, "Unauthorized");
                }

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

        private async Task<JObject> PostPlayNextSong(JToken dontCare)
        {
            try
            {
                await this.library.PlayNextSongAsync(this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(401, "Unauthorized");
            }

            return CreateResponse(200, "Ok");
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
                    try
                    {
                        await this.library.PlaySongAsync(entry.Index, this.accessToken);
                    }

                    catch (AccessException)
                    {
                        return CreateResponse(401, "Unauthorized");
                    }

                    return CreateResponse(200, "Playing song");
                }

                return CreateResponse(404, "Playlist entry not found");
            }

            return CreateResponse(400, "Malformed GUID");
        }

        private async Task<JObject> PostPlayPreviousSong(JToken dontCare)
        {
            try
            {
                await this.library.PlayPreviousSongAsync(this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(401, "Unauthorized");
            }

            return CreateResponse(200, "Ok");
        }

        private Task<JObject> PostRemovePlaylistSong(JToken parameters)
        {
            Guid songGuid = Guid.Parse(parameters["entryGuid"].ToString());

            PlaylistEntry entry = this.library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry != null)
            {
                this.library.RemoveFromPlaylist(new[] { entry.Index }, this.accessToken);

                return Task.FromResult(CreateResponse(200, "Ok"));
            }

            return Task.FromResult(CreateResponse(400, "Guid not found"));
        }

        private async Task PushAccessPermission(AccessPermission accessPermission)
        {
            var content = new JObject
            {
                {"accessPermission", accessPermission.ToString()}
            };

            JObject message = CreatePush("update-access-permission", content);

            await this.SendMessage(message);
        }

        private async Task PushPlaybackState(AudioPlayerState state)
        {
            var content = new JObject
            {
                {"state", state.ToString()}
            };

            JObject message = CreatePush("update-playback-state", content);

            await this.SendMessage(message);
        }

        private async Task PushPlaylist(Playlist playlist)
        {
            JObject content = MobileHelper.SerializePlaylist(playlist);

            JObject message = CreatePush("update-current-playlist", content);

            await this.SendMessage(message);
        }

        private async Task PushPlaylistIndex(int? index)
        {
            var content = new JObject
            {
                { "index", index }
            };

            JObject message = CreatePush("update-current-index", content);

            await this.SendMessage(message);
        }

        private async Task SendMessage(JObject content)
        {
            byte[] message = await MobileHelper.PackMessage(content);

            await this.gate.WaitAsync();

            try
            {
                await this.socket.SendAsync(message);
            }

            catch (Exception)
            {
                this.disconnected.OnNext(Unit.Default);
            }

            finally
            {
                this.gate.Release();
            }
        }
    }
}