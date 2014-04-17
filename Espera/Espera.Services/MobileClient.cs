using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Network;
using Newtonsoft.Json.Linq;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.Services
{
    /// <summary>
    /// Represents one mobile endpoint and handles the interaction.
    /// </summary>
    public class MobileClient : IDisposable, IEnableLogger
    {
        private readonly Subject<Unit> disconnected;
        private readonly CompositeDisposable disposable;
        private readonly SemaphoreSlim gate;
        private readonly Library library;
        private readonly Dictionary<string, Func<JToken, Task<ResponseInfo>>> messageActionMap;
        private readonly TcpClient socket;
        private Guid accessToken;

        public MobileClient(TcpClient socket, Library library)
        {
            if (socket == null)
                Throw.ArgumentNullException(() => socket);

            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.socket = socket;
            this.library = library;

            this.disposable = new CompositeDisposable();
            this.gate = new SemaphoreSlim(1, 1);
            this.disconnected = new Subject<Unit>();

            this.messageActionMap = new Dictionary<string, Func<JToken, Task<ResponseInfo>>>
            {
                {"get-connection-info", this.GetConnectionInfo},
                {"get-library-content", this.GetLibraryContent},
                {"post-playlist-song", this.PostPlaylistSong},
                {"post-play-instantly", this.PostPlayInstantly},
                {"get-current-playlist", this.GetCurrentPlaylist},
                {"post-play-playlist-song", this.PostPlayPlaylistSong},
                {"post-continue-song", this.PostContinueSong},
                {"post-pause-song", this.PostPauseSong},
                {"post-play-next-song", this.PostPlayNextSong},
                {"post-play-previous-song", this.PostPlayPreviousSong},
                {"post-remove-playlist-song", this.PostRemovePlaylistSong},
                {"move-playlist-song-up", this.MovePlaylistSongUp},
                {"move-playlist-song-down", this.MovePlaylistSongDown},
                {"get-volume", this.GetVolume},
                {"set-volume", this.SetVolume},
                {"vote-for-song", this.VoteForSong}
            };
        }

        public IObservable<Unit> Disconnected
        {
            get { return this.disconnected.AsObservable(); }
        }

        public void Dispose()
        {
            this.socket.Close();
            this.gate.Dispose();
            this.disconnected.Dispose();
            this.disposable.Dispose();
        }

        public void ListenAsync()
        {
            Observable.Defer(() => this.socket.GetStream().ReadNextMessageAsync().ToObservable())
                .Repeat()
                .TakeWhile(x => x != null)
                // If we don't do this, the application will throw up whenever we are manipulating a
                // collection that is surfaced to the UI Yes, this is astoundingly stupid
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(async message =>
                {
                    var request = message.Payload.ToObject<RequestInfo>();
                    var responseMessage = new NetworkMessage { MessageType = NetworkMessageType.Response };

                    if (request.RequestAction == null)
                    {
                        this.Log().Warn("Mobile client with access token {0} sent a request without specifiying an action!", this.accessToken);

                        ResponseInfo response = CreateResponse(ResponseStatus.MalformedRequest);
                        response.RequestId = request.RequestId;
                        responseMessage.Payload = JObject.FromObject(response);

                        await this.SendMessage(responseMessage);

                        return;
                    }

                    Func<JToken, Task<ResponseInfo>> action;

                    if (this.messageActionMap.TryGetValue(request.RequestAction, out action))
                    {
                        bool isFatalRequest = false;
                        try
                        {
                            ResponseInfo response = await action(request.Parameters);

                            response.RequestId = request.RequestId;
                            responseMessage.Payload = JObject.FromObject(response);

                            await this.SendMessage(responseMessage);
                        }

                        catch (Exception ex)
                        {
                            this.Log().ErrorException(string.Format(
                                "Mobile client with access token {0} sent a request that caused an exception", this.accessToken), ex);
                            if (Debugger.IsAttached)
                            {
                                Debugger.Break();
                            }

                            isFatalRequest = true;
                        }

                        if (isFatalRequest)
                        {
                            ResponseInfo response = CreateResponse(ResponseStatus.Fatal);
                            response.RequestId = request.RequestId;
                            responseMessage.Payload = JObject.FromObject(response);

                            // Client what are you doing? Client stahp!
                            await this.SendMessage(responseMessage);
                        }
                    }
                }, ex => this.disconnected.OnNext(Unit.Default), () => this.disconnected.OnNext(Unit.Default))
                .DisposeWith(this.disposable);
        }

        private static NetworkMessage CreatePush(string action, JObject content)
        {
            var message = new NetworkMessage
            {
                MessageType = NetworkMessageType.Push,
                Payload = JObject.FromObject(new PushInfo
                {
                    Content = content,
                    PushAction = action
                })
            };

            return message;
        }

        private static ResponseInfo CreateResponse(ResponseStatus status, JObject content = null)
        {
            return CreateResponse(status, null, content);
        }

        private static ResponseInfo CreateResponse(ResponseStatus status, string message, JObject content = null)
        {
            return new ResponseInfo
            {
                Status = status,
                Message = message,
                Content = content,
            };
        }

        private async Task<ResponseInfo> GetConnectionInfo(JToken parameters)
        {
            Guid deviceId = Guid.Parse(parameters["deviceId"].ToString());

            this.accessToken = this.library.RemoteAccessControl.RegisterRemoteAccessToken(deviceId);
            this.Log().Info("Registering new mobile client with access token {0}", this.accessToken);

            string password = parameters["password"].Value<string>();

            if (password != null)
            {
                try
                {
                    this.library.RemoteAccessControl.UpgradeRemoteAccess(this.accessToken, password);
                }

                catch (WrongPasswordException)
                {
                    return CreateResponse(ResponseStatus.WrongPassword);
                }
            }

            Version serverVersion = Assembly.GetExecutingAssembly().GetName().Version;
            AccessPermission accessPermission = await this.library.RemoteAccessControl.ObserveAccessPermission(this.accessToken).FirstAsync();

            // This is stupid
            NetworkAccessPermission permission = accessPermission == AccessPermission.Admin ? NetworkAccessPermission.Admin : NetworkAccessPermission.Guest;

            var connectionInfo = new ConnectionInfo
            {
                AccessPermission = permission,
                ServerVersion = serverVersion
            };

            this.SetupPushNotifications();

            return CreateResponse(ResponseStatus.Success, null, JObject.FromObject(connectionInfo));
        }

        private async Task<ResponseInfo> GetCurrentPlaylist(JToken dontCare)
        {
            Playlist playlist = this.library.CurrentPlaylist;
            int? remainingVotes = await this.library.RemoteAccessControl.ObserveRemainingVotes(this.accessToken).FirstAsync();
            AudioPlayerState playbackState = await this.library.PlaybackState.FirstAsync();

            JObject content = MobileHelper.SerializePlaylist(playlist, remainingVotes, playbackState);

            return CreateResponse(ResponseStatus.Success, null, content);
        }

        private Task<ResponseInfo> GetLibraryContent(JToken dontCare)
        {
            JObject content = MobileHelper.SerializeSongs(this.library.Songs);

            return Task.FromResult(CreateResponse(ResponseStatus.Success, null, content));
        }

        private Task<ResponseInfo> GetVolume(JToken dontCare)
        {
            float volume = this.library.Volume;

            var response = JObject.FromObject(new
            {
                volume
            });

            return Task.FromResult(CreateResponse(ResponseStatus.Success, response));
        }

        private Task<ResponseInfo> MovePlaylistSongDown(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (!valid)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID"));
            }

            PlaylistEntry entry = this.library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.NotFound, "Playlist entry not found"));
            }

            try
            {
                this.library.MovePlaylistSongDown(entry.Index, this.accessToken);
            }

            catch (AccessException)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.Unauthorized));
            }

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private Task<ResponseInfo> MovePlaylistSongUp(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (!valid)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID"));
            }

            PlaylistEntry entry = this.library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.NotFound));
            }

            try
            {
                this.library.MovePlaylistSongUp(entry.Index, this.accessToken);
            }

            catch (AccessException)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.Unauthorized));
            }

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private async Task<ResponseInfo> PostContinueSong(JToken content)
        {
            try
            {
                await this.library.ContinueSongAsync(this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized);
            }

            return CreateResponse(ResponseStatus.Success);
        }

        private async Task<ResponseInfo> PostPauseSong(JToken dontCare)
        {
            try
            {
                await this.library.PauseSongAsync(this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized);
            }

            return CreateResponse(ResponseStatus.Success);
        }

        private async Task<ResponseInfo> PostPlayInstantly(JToken parameters)
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
                    return CreateResponse(ResponseStatus.MalformedRequest, "One or more GUIDs are malformed");
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

            if (guids.Count != songs.Count)
            {
                return CreateResponse(ResponseStatus.NotFound, "One or more songs could not be found");
            }

            try
            {
                await this.library.PlayInstantlyAsync(songs, this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized);
            }

            return CreateResponse(ResponseStatus.Success);
        }

        private Task<ResponseInfo> PostPlaylistSong(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["songGuid"].ToString(), out songGuid);

            if (!valid)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID"));
            }

            LocalSong song = this.library.Songs.FirstOrDefault(x => x.Guid == songGuid);

            if (song == null)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.NotFound, "Song not found"));
            }

            this.library.AddSongToPlaylist(song);

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private async Task<ResponseInfo> PostPlayNextSong(JToken dontCare)
        {
            try
            {
                await this.library.PlayNextSongAsync(this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized);
            }

            return CreateResponse(ResponseStatus.Success);
        }

        private async Task<ResponseInfo> PostPlayPlaylistSong(JToken parameters)
        {
            Guid songGuid;
            bool valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (!valid)
            {
                return CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID");
            }

            PlaylistEntry entry = this.library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null)
            {
                return CreateResponse(ResponseStatus.NotFound, "Playlist entry not found");
            }

            try
            {
                await this.library.PlaySongAsync(entry.Index, this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized);
            }

            return CreateResponse(ResponseStatus.Success);
        }

        private async Task<ResponseInfo> PostPlayPreviousSong(JToken dontCare)
        {
            try
            {
                await this.library.PlayPreviousSongAsync(this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized);
            }

            return CreateResponse(ResponseStatus.Success);
        }

        private Task<ResponseInfo> PostRemovePlaylistSong(JToken parameters)
        {
            Guid songGuid = Guid.Parse(parameters["entryGuid"].ToString());

            PlaylistEntry entry = this.library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.NotFound, "Guid not found"));
            }

            this.library.RemoveFromPlaylist(new[] { entry.Index }, this.accessToken);

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private async Task PushAccessPermission(AccessPermission accessPermission)
        {
            var content = JObject.FromObject(new
            {
                accessPermission
            });

            NetworkMessage message = CreatePush("update-access-permission", content);

            await this.SendMessage(message);
        }

        private async Task PushPlaybackState(AudioPlayerState state)
        {
            var content = JObject.FromObject(new
            {
                state
            });

            NetworkMessage message = CreatePush("update-playback-state", content);

            await this.SendMessage(message);
        }

        private async Task PushPlaylist(Playlist playlist, int? remainingVotes, AudioPlayerState state)
        {
            JObject content = MobileHelper.SerializePlaylist(playlist, remainingVotes, state);

            NetworkMessage message = CreatePush("update-current-playlist", content);

            await this.SendMessage(message);
        }

        private async Task PushRemainingVotes(int? remainingVotes)
        {
            var content = JObject.FromObject(new
            {
                remainingVotes
            });

            NetworkMessage message = CreatePush("update-remaining-votes", content);

            await this.SendMessage(message);
        }

        private async Task SendMessage(NetworkMessage content)
        {
            byte[] message = await NetworkHelpers.PackMessageAsync(content);

            await this.gate.WaitAsync();

            try
            {
                await this.socket.GetStream().WriteAsync(message, 0, message.Length);
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

        private void SetupPushNotifications()
        {
            this.library.CurrentPlaylistChanged
                .Merge(this.library.CurrentPlaylistChanged
                    .StartWith(this.library.CurrentPlaylist)
                    .Select(x => x.Changed().Select(y => x))
                    .Switch())
                .Merge(this.library.CurrentPlaylistChanged
                    .StartWith(this.library.CurrentPlaylist)
                    .Select(x => x.CurrentSongIndex.Skip(1).Select(y => x))
                    .Switch())
                .CombineLatest(this.library.RemoteAccessControl.ObserveRemainingVotes(this.accessToken),
                    this.library.PlaybackState, Tuple.Create)
                .Subscribe(x => this.PushPlaylist(x.Item1, x.Item2, x.Item3))
                .DisposeWith(this.disposable);

            this.library.PlaybackState.Skip(1)
                .Subscribe(x => this.PushPlaybackState(x))
                .DisposeWith(this.disposable);

            this.library.RemoteAccessControl.ObserveAccessPermission(this.accessToken)
                .Skip(1)
                .Subscribe(x => this.PushAccessPermission(x))
                .DisposeWith(this.disposable);

            this.library.RemoteAccessControl.ObserveRemainingVotes(this.accessToken)
                .Skip(1)
                .Subscribe(x => this.PushRemainingVotes(x))
                .DisposeWith(this.disposable);
        }

        private Task<ResponseInfo> SetVolume(JToken parameters)
        {
            var volume = parameters["volume"].ToObject<float>();

            if (volume < 0 || volume > 1.0)
                return Task.FromResult(CreateResponse(ResponseStatus.MalformedRequest, "Volume must be between 0 and 1"));

            try
            {
                this.library.SetVolume(volume, this.accessToken);
            }

            catch (AccessException)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.Unauthorized));
            }

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private async Task<ResponseInfo> VoteForSong(JToken parameters)
        {
            int? remainingVotes = await this.library.RemoteAccessControl.ObserveRemainingVotes(this.accessToken).FirstAsync();

            if (remainingVotes == null)
            {
                return CreateResponse(ResponseStatus.NotSupported, "Voting isn't supported");
            }

            if (remainingVotes == 0)
            {
                return CreateResponse(ResponseStatus.Rejected, "Not enough votes left");
            }

            Guid songGuid;
            bool valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (!valid)
            {
                return CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID");
            }

            Playlist playlist = this.library.CurrentPlaylist;
            PlaylistEntry entry = playlist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null)
            {
                return CreateResponse(ResponseStatus.NotFound, "Playlist entry not found");
            }

            if (this.library.RemoteAccessControl.IsVoteRegistered(this.accessToken, entry))
            {
                return CreateResponse(ResponseStatus.Rejected, "Vote already registered");
            }

            if (playlist.CurrentSongIndex.Value.HasValue && entry.Index <= playlist.CurrentSongIndex.Value)
            {
                return CreateResponse(ResponseStatus.Rejected, "Vote rejected");
            }

            try
            {
                this.library.VoteForPlaylistEntry(entry.Index, this.accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized, "Unauthorized");
            }

            return CreateResponse(ResponseStatus.Success);
        }
    }
}