using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rareform.Validation;
using ReactiveMarrow;
using ReactiveUI;

namespace Espera.Core.Mobile
{
    /// <summary>
    /// Represents one mobile endpoint and handles the interaction.
    /// </summary>
    public class MobileClient : IDisposable, IEnableLogger
    {
        private readonly Subject<Unit> disconnected;
        private readonly CompositeDisposable disposable;
        private readonly TcpClient fileSocket;
        private readonly SemaphoreSlim gate;
        private readonly Library library;
        private readonly Dictionary<RequestAction, Func<JToken, Task<ResponseInfo>>> messageActionMap;
        private readonly TcpClient socket;
        private Guid accessToken;
        private IObservable<SongTransferMessage> songTransfers;

        public MobileClient(TcpClient socket, TcpClient fileSocket, Library library)
        {
            if (socket == null)
                Throw.ArgumentNullException(() => socket);

            if (fileSocket == null)
                Throw.ArgumentNullException(() => socket);

            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.socket = socket;
            this.fileSocket = fileSocket;
            this.library = library;

            this.disposable = new CompositeDisposable();
            this.gate = new SemaphoreSlim(1, 1);
            this.disconnected = new Subject<Unit>();

            this.messageActionMap = new Dictionary<RequestAction, Func<JToken, Task<ResponseInfo>>>
            {
                {RequestAction.GetConnectionInfo, this.GetConnectionInfo},
                {RequestAction.GetLibraryContent, this.GetLibraryContent},
                {RequestAction.AddPlaylistSongs, this.AddPlaylistSongs},
                {RequestAction.AddPlaylistSongsNow, this.AddPlaylistSongsNow},
                {RequestAction.GetCurrentPlaylist, this.GetCurrentPlaylist},
                {RequestAction.PlayPlaylistSong, this.PlayPlaylistSong},
                {RequestAction.ContinueSong, this.ContinueSong},
                {RequestAction.PauseSong, this.PauseSong},
                {RequestAction.PlayNextSong, this.PlayNextSong},
                {RequestAction.PlayPreviousSong, this.PlayPreviousSong},
                {RequestAction.RemovePlaylistSong, this.PostRemovePlaylistSong},
                {RequestAction.MovePlaylistSongUp, this.MovePlaylistSongUp},
                {RequestAction.MovePlaylistSongDown, this.MovePlaylistSongDown},
                {RequestAction.GetVolume, this.GetVolume},
                {RequestAction.SetVolume, this.SetVolume},
                {RequestAction.VoteForSong, this.VoteForSong},
                {RequestAction.QueueRemoteSong, this.QueueRemoteSong},
                {RequestAction.SetCurrentTime, this.SetCurrentTime}
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
            Observable.FromAsync(() => this.socket.GetStream().ReadNextMessageAsync())
                .Repeat()
                .TakeWhile(x => x != null)
                // If we don't do this, the application will throw up whenever we are manipulating a
                // collection that is surfaced to the UI Yes, this is astoundingly stupid
                .ObserveOn(RxApp.MainThreadScheduler)
                .SelectMany(async message =>
                {
                    RequestInfo request;

                    try
                    {
                        request = message.Payload.ToObject<RequestInfo>();
                    }

                    catch (JsonException ex)
                    {
                        this.Log().ErrorException("Mobile client with access token {0} sent a malformed request", ex);
                        return Unit.Default;
                    }

                    var responseMessage = new NetworkMessage { MessageType = NetworkMessageType.Response };

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

                        return Unit.Default;
                    }

                    return Unit.Default;
                }).Subscribe(_ => { }, ex => this.disconnected.OnNext(Unit.Default), () => this.disconnected.OnNext(Unit.Default))
                .DisposeWith(this.disposable);

            var transfers = Observable.FromAsync(() => this.fileSocket.GetStream().ReadNextFileTransferMessageAsync())
                .Repeat()
                .Retry()
                .TakeWhile(x => x != null)
                .Publish();
            transfers.Connect().DisposeWith(this.disposable);

            this.songTransfers = transfers;
        }

        private static NetworkMessage CreatePushMessage(PushAction action, JObject content)
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

        private Task<ResponseInfo> AddPlaylistSongs(JToken parameters)
        {
            IEnumerable<Song> songs;
            ResponseInfo response;

            bool areValid = this.TryValidateSongGuids(parameters["guids"].Select(x => x.ToString()), out songs, out response);

            if (areValid)
            {
                try
                {
                    this.library.AddSongsToPlaylist(songs, this.accessToken);
                }

                catch (AccessException)
                {
                    return Task.FromResult(CreateResponse(ResponseStatus.Unauthorized));
                }

                return Task.FromResult(CreateResponse(ResponseStatus.Success));
            }

            return Task.FromResult(response);
        }

        private async Task<ResponseInfo> AddPlaylistSongsNow(JToken parameters)
        {
            IEnumerable<Song> songs;
            ResponseInfo response;

            bool areValid = this.TryValidateSongGuids(parameters["guids"].Select(x => x.ToString()), out songs, out response);

            if (areValid)
            {
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

            return response;
        }

        private async Task<ResponseInfo> ContinueSong(JToken content)
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

        private async Task<ResponseInfo> GetConnectionInfo(JToken parameters)
        {
            Guid deviceId = Guid.Parse(parameters["deviceId"].ToString());

            this.accessToken = this.library.RemoteAccessControl.RegisterRemoteAccessToken(deviceId);
            this.Log().Info("Registering new mobile client with access token {0}", this.accessToken);

            if (this.library.RemoteAccessControl.IsRemoteAccessReallyLocked())
            {
                var password = parameters["password"].Value<string>();

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

            TimeSpan currentTime = await this.library.CurrentPlaybackTime.FirstAsync();
            TimeSpan totalTime = await this.library.TotalTime.FirstAsync();
            JObject content = MobileHelper.SerializePlaylist(playlist, remainingVotes, playbackState, currentTime, totalTime);

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
                this.library.MovePlaylistSong(entry.Index, entry.Index + 1, this.accessToken);
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
                this.library.MovePlaylistSong(entry.Index, entry.Index - 1, this.accessToken);
            }

            catch (AccessException)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.Unauthorized));
            }

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private async Task<ResponseInfo> PauseSong(JToken dontCare)
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

        private async Task<ResponseInfo> PlayNextSong(JToken dontCare)
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

        private async Task<ResponseInfo> PlayPlaylistSong(JToken parameters)
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

        private async Task<ResponseInfo> PlayPreviousSong(JToken dontCare)
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

            NetworkMessage message = CreatePushMessage(PushAction.UpdateAccessPermission, content);

            await this.SendMessage(message);
        }

        private Task PushCurrentPlaybackTime(TimeSpan currentPlaybackTime)
        {
            var content = JObject.FromObject(new
            {
                currentPlaybackTime
            });

            NetworkMessage message = CreatePushMessage(PushAction.UpdateCurrentPlaybackTime, content);

            return this.SendMessage(message);
        }

        private async Task PushPlaybackState(AudioPlayerState state)
        {
            var content = JObject.FromObject(new
            {
                state
            });

            NetworkMessage message = CreatePushMessage(PushAction.UpdatePlaybackState, content);

            await this.SendMessage(message);
        }

        private async Task PushPlaylist(Playlist playlist, int? remainingVotes, AudioPlayerState state)
        {
            JObject content = MobileHelper.SerializePlaylist(playlist, remainingVotes, state,
                await this.library.CurrentPlaybackTime.FirstAsync(),
                await this.library.TotalTime.FirstAsync());

            NetworkMessage message = CreatePushMessage(PushAction.UpdateCurrentPlaylist, content);

            await this.SendMessage(message);
        }

        private async Task PushRemainingVotes(int? remainingVotes)
        {
            var content = JObject.FromObject(new
            {
                remainingVotes
            });

            NetworkMessage message = CreatePushMessage(PushAction.UpdateRemainingVotes, content);

            await this.SendMessage(message);
        }

        private Task<ResponseInfo> QueueRemoteSong(JToken parameters)
        {
            var transferInfo = parameters.ToObject<SongTransferInfo>();

            IObservable<byte[]> data = this.songTransfers.FirstAsync(x => x.TransferId == transferInfo.TransferId).Select(x => x.Data);

            var song = MobileSong.Create(transferInfo.Metadata, data);

            this.library.AddGuestSongToPlaylist(song, this.accessToken);

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private async Task SendMessage(NetworkMessage content)
        {
            byte[] message;
            using (MeasureHelper.Measure())
            {
                message = await NetworkHelpers.PackMessageAsync(content);
            }

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

        private Task<ResponseInfo> SetCurrentTime(JToken parameters)
        {
            var time = parameters["time"].ToObject<TimeSpan>();

            try
            {
                this.library.SetCurrentTime(time, this.accessToken);
            }

            catch (AccessException)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.Unauthorized));
            }

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private void SetupPushNotifications()
        {
            this.library.WhenAnyValue(x => x.CurrentPlaylist).Skip(1)
                .Merge(this.library.WhenAnyValue(x => x.CurrentPlaylist)
                    .Select(x => x.Changed().Select(y => x))
                    .Switch())
                .Merge(this.library.WhenAnyValue(x => x.CurrentPlaylist)
                    .Select(x => x.WhenAnyValue(y => y.CurrentSongIndex).Skip(1).Select(y => x))
                    .Switch())
                .CombineLatest(this.library.RemoteAccessControl.ObserveRemainingVotes(this.accessToken),
                    this.library.PlaybackState, Tuple.Create)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => this.PushPlaylist(x.Item1, x.Item2, x.Item3))
                .DisposeWith(this.disposable);

            this.library.PlaybackState.Skip(1)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => this.PushPlaybackState(x))
                .DisposeWith(this.disposable);

            this.library.RemoteAccessControl.ObserveAccessPermission(this.accessToken)
                .Skip(1)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => this.PushAccessPermission(x))
                .DisposeWith(this.disposable);

            this.library.RemoteAccessControl.ObserveRemainingVotes(this.accessToken)
                .Skip(1)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => this.PushRemainingVotes(x))
                .DisposeWith(this.disposable);

            TimeSpan lastTime = TimeSpan.Zero;
            // We can assume that, if the total time difference exceeds two seconds, the time change
            // is from an external source (e.g the user clicked on the time slider)
            this.library.CurrentPlaybackTime
                .Select(x => Math.Abs(lastTime.TotalSeconds - x.TotalSeconds) >= 2 ? Tuple.Create(x, true) : Tuple.Create(x, false))
                .Do(x => lastTime = x.Item1)
                .Where(x => x.Item2)
                .Select(x => x.Item1)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => this.PushCurrentPlaybackTime(x))
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

        private bool TryValidateSongGuids(IEnumerable<string> guidStrings, out IEnumerable<Song> foundSongs, out ResponseInfo responseInfo)
        {
            var guids = new List<Guid>();

            foreach (string guidString in guidStrings)
            {
                Guid guid;

                bool valid = Guid.TryParse(guidString, out guid);

                if (valid)
                {
                    guids.Add(guid);
                }

                else
                {
                    responseInfo = CreateResponse(ResponseStatus.MalformedRequest, "One or more GUIDs are malformed");
                    foundSongs = null;
                    return false;
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
                responseInfo = CreateResponse(ResponseStatus.NotFound, "One or more songs could not be found");
                foundSongs = null;
                return false;
            }

            responseInfo = null;
            foundSongs = songs;
            return true;
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

            if (playlist.CurrentSongIndex.HasValue && entry.Index <= playlist.CurrentSongIndex.Value)
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