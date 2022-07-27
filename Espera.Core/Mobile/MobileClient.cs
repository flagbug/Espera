using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Espera.Core.Audio;
using Espera.Core.Management;

namespace Espera.Core.Mobile
{
    /// <summary>
    ///     Represents one mobile endpoint and handles the interaction.
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
        private readonly Subject<Unit> videoPlayerToggleRequest;
        private Guid accessToken;
        private IReadOnlyList<SoundCloudSong> lastSoundCloudRequest;
        private IReadOnlyList<YoutubeSong> lastYoutubeRequest;
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

            disposable = new CompositeDisposable();
            gate = new SemaphoreSlim(1, 1);
            disconnected = new Subject<Unit>();
            lastSoundCloudRequest = new List<SoundCloudSong>();
            lastYoutubeRequest = new List<YoutubeSong>();
            videoPlayerToggleRequest = new Subject<Unit>();

            messageActionMap = new Dictionary<RequestAction, Func<JToken, Task<ResponseInfo>>>
            {
                { RequestAction.GetConnectionInfo, GetConnectionInfo },
                { RequestAction.ToggleYoutubePlayer, ToggleVideoPlayer },
                { RequestAction.GetLibraryContent, GetLibraryContent },
                { RequestAction.GetSoundCloudSongs, GetSoundCloudSongs },
                { RequestAction.GetYoutubeSongs, GetYoutubeSongs },
                { RequestAction.AddPlaylistSongs, AddPlaylistSongs },
                { RequestAction.AddPlaylistSongsNow, AddPlaylistSongsNow },
                { RequestAction.GetCurrentPlaylist, GetCurrentPlaylist },
                { RequestAction.PlayPlaylistSong, PlayPlaylistSong },
                { RequestAction.ContinueSong, ContinueSong },
                { RequestAction.PauseSong, PauseSong },
                { RequestAction.PlayNextSong, PlayNextSong },
                { RequestAction.PlayPreviousSong, PlayPreviousSong },
                { RequestAction.RemovePlaylistSong, PostRemovePlaylistSong },
                { RequestAction.MovePlaylistSongUp, MovePlaylistSongUp },
                { RequestAction.MovePlaylistSongDown, MovePlaylistSongDown },
                { RequestAction.GetVolume, GetVolume },
                { RequestAction.SetVolume, SetVolume },
                { RequestAction.VoteForSong, VoteForSong },
                { RequestAction.QueueRemoteSong, QueueRemoteSong },
                { RequestAction.SetCurrentTime, SetCurrentTime }
            };
        }

        public IObservable<Unit> Disconnected => disconnected.AsObservable();

        /// <summary>
        ///     Signals when the mobile client wants to toggle the visibility of the video player.
        /// </summary>
        public IObservable<Unit> VideoPlayerToggleRequest => videoPlayerToggleRequest.AsObservable();

        public void Dispose()
        {
            socket.Close();
            gate.Dispose();
            disposable.Dispose();
        }

        public void ListenAsync()
        {
            Observable.FromAsync(() => socket.GetStream().ReadNextMessageAsync())
                .Repeat()
                .LoggedCatch(this, null, "Message connection was closed by the remote device or the connection failed")
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
                        this.Log().ErrorException(
                            string.Format("Mobile client with access token {0} sent a malformed request", accessToken),
                            ex);
                        return Unit.Default;
                    }

                    var responseMessage = new NetworkMessage { MessageType = NetworkMessageType.Response };

                    Func<JToken, Task<ResponseInfo>> action;

                    if (messageActionMap.TryGetValue(request.RequestAction, out action))
                    {
                        var isFatalRequest = false;
                        try
                        {
                            var response = await action(request.Parameters);
                            response.RequestId = request.RequestId;

                            responseMessage.Payload = await Task.Run(() => JObject.FromObject(response));

                            await SendMessage(responseMessage);
                        }

                        catch (Exception ex)
                        {
                            this.Log().ErrorException(
                                string.Format(
                                    "Mobile client with access token {0} sent a request that caused an exception",
                                    accessToken), ex);
                            if (Debugger.IsAttached) Debugger.Break();

                            isFatalRequest = true;
                        }

                        if (isFatalRequest)
                        {
                            var response = CreateResponse(ResponseStatus.Fatal);
                            response.RequestId = request.RequestId;
                            responseMessage.Payload = JObject.FromObject(response);

                            // Client what are you doing? Client stahp!
                            await SendMessage(responseMessage);
                        }

                        return Unit.Default;
                    }

                    return Unit.Default;
                })
                .Finally(() => disconnected.OnNext(Unit.Default))
                .Subscribe()
                .DisposeWith(disposable);

            var transfers = Observable.FromAsync(() => fileSocket.GetStream().ReadNextFileTransferMessageAsync())
                .Repeat()
                .LoggedCatch(this, null,
                    "File transfer connection was closed by the remote device or the connection failed")
                .TakeWhile(x => x != null)
                .Publish();
            transfers.Connect().DisposeWith(disposable);

            songTransfers = transfers;
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
                Content = content
            };
        }

        private async Task<ResponseInfo> AddPlaylistSongs(JToken parameters)
        {
            IEnumerable<Song> songs;
            ResponseInfo response;

            var areValid = TryValidateSongGuids(parameters["guids"].Select(x => x.ToString()), out songs, out response);

            if (areValid)
            {
                var permission = await library.RemoteAccessControl.ObserveAccessPermission(accessToken).FirstAsync();

                if (permission == AccessPermission.Guest)
                {
                    var remainingVotes =
                        await library.RemoteAccessControl.ObserveRemainingVotes(accessToken).FirstAsync();

                    if (remainingVotes == null)
                        return CreateResponse(ResponseStatus.NotSupported, "Voting isn't supported");

                    if (remainingVotes == 0) return CreateResponse(ResponseStatus.Rejected, "Not enough votes left");
                }

                if (permission == AccessPermission.Admin)
                {
                    library.AddSongsToPlaylist(songs, accessToken);
                }

                else
                {
                    if (songs.Count() > 1)
                        return CreateResponse(ResponseStatus.Unauthorized, "Guests can't add more than one song");

                    library.AddGuestSongToPlaylist(songs.First(), accessToken);
                }

                return CreateResponse(ResponseStatus.Success);
            }

            return response;
        }

        private async Task<ResponseInfo> AddPlaylistSongsNow(JToken parameters)
        {
            IEnumerable<Song> songs;
            ResponseInfo response;

            var areValid = TryValidateSongGuids(parameters["guids"].Select(x => x.ToString()), out songs, out response);

            if (areValid)
            {
                try
                {
                    await library.PlayInstantlyAsync(songs, accessToken);
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
                await library.ContinueSongAsync(accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized);
            }

            return CreateResponse(ResponseStatus.Success);
        }

        private async Task<ResponseInfo> GetConnectionInfo(JToken parameters)
        {
            var deviceId = Guid.Parse(parameters["deviceId"].ToString());

            accessToken = library.RemoteAccessControl.RegisterRemoteAccessToken(deviceId);
            this.Log().Info("Registering new mobile client with access token {0}", accessToken);

            if (library.RemoteAccessControl.IsRemoteAccessReallyLocked)
            {
                var password = parameters["password"].Value<string>();

                if (password != null)
                    try
                    {
                        library.RemoteAccessControl.UpgradeRemoteAccess(accessToken, password);
                    }

                    catch (WrongPasswordException)
                    {
                        return CreateResponse(ResponseStatus.WrongPassword);
                    }
            }

            var accessPermission = await library.RemoteAccessControl.ObserveAccessPermission(accessToken).FirstAsync();

            // This is stupid
            var permission = accessPermission == AccessPermission.Admin
                ? NetworkAccessPermission.Admin
                : NetworkAccessPermission.Guest;

            var remainingVotes = await library.RemoteAccessControl.ObserveRemainingVotes(accessToken).FirstAsync();

            var guestSystemInfo = new GuestSystemInfo
            {
                IsEnabled = remainingVotes.HasValue
            };

            if (remainingVotes.HasValue) guestSystemInfo.RemainingVotes = remainingVotes.Value;

            var connectionInfo = new ConnectionInfo
            {
                AccessPermission = permission,
                ServerVersion = AppInfo.Version,
                GuestSystemInfo = guestSystemInfo
            };

            SetupPushNotifications();

            return CreateResponse(ResponseStatus.Success, null, JObject.FromObject(connectionInfo));
        }

        private async Task<ResponseInfo> GetCurrentPlaylist(JToken dontCare)
        {
            var playlist = library.CurrentPlaylist;
            var playbackState = await library.PlaybackState.FirstAsync();

            var currentTime = await library.CurrentPlaybackTime.FirstAsync();
            var totalTime = await library.TotalTime.FirstAsync();
            var content = MobileHelper.SerializePlaylist(playlist, playbackState, currentTime, totalTime);

            return CreateResponse(ResponseStatus.Success, null, content);
        }

        private async Task<ResponseInfo> GetLibraryContent(JToken dontCare)
        {
            var content = await Task.Run(() => MobileHelper.SerializeSongs(library.Songs));

            return CreateResponse(ResponseStatus.Success, null, content);
        }

        private async Task<ResponseInfo> GetSoundCloudSongs(JToken parameters)
        {
            var searchTerm = parameters["searchTerm"].ToObject<string>();

            try
            {
                var requestCache = Locator.Current.GetService<IBlobCache>(BlobCacheKeys.RequestCacheContract);
                var songFinder = new SoundCloudSongFinder(requestCache);

                var songs = await songFinder.GetSongsAsync(searchTerm);

                // Cache the latest SoundCloud search request, so we can find the songs by GUID when
                // we add one to the playlist later
                lastSoundCloudRequest = songs;

                var content = MobileHelper.SerializeSongs(songs);

                return CreateResponse(ResponseStatus.Success, content);
            }

            catch (NetworkSongFinderException)
            {
                return CreateResponse(ResponseStatus.Failed, "Couldn't retrieve any SoundCloud songs");
            }
        }

        private Task<ResponseInfo> GetVolume(JToken dontCare)
        {
            var volume = library.Volume;

            var response = JObject.FromObject(new
            {
                volume
            });

            return Task.FromResult(CreateResponse(ResponseStatus.Success, response));
        }

        private async Task<ResponseInfo> GetYoutubeSongs(JToken parameters)
        {
            var searchTerm = parameters["searchTerm"].ToObject<string>();

            try
            {
                var requestCache = Locator.Current.GetService<IBlobCache>(BlobCacheKeys.RequestCacheContract);
                var songFinder = new YoutubeSongFinder(requestCache);

                var songs = await songFinder.GetSongsAsync(searchTerm);

                // Cache the latest YouTube search request, so we can find the songs by GUID when we
                // add one to the playlist later
                lastYoutubeRequest = songs;

                var content = MobileHelper.SerializeSongs(songs);

                return CreateResponse(ResponseStatus.Success, content);
            }

            catch (NetworkSongFinderException)
            {
                return CreateResponse(ResponseStatus.Failed, "Couldn't retrieve any YouTube songs");
            }

            ;
        }

        private Task<ResponseInfo> MovePlaylistSongDown(JToken parameters)
        {
            Guid songGuid;
            var valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (!valid) return Task.FromResult(CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID"));

            var entry = library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null)
                return Task.FromResult(CreateResponse(ResponseStatus.NotFound, "Playlist entry not found"));

            try
            {
                library.MovePlaylistSong(entry.Index, entry.Index + 1, accessToken);
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
            var valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (!valid) return Task.FromResult(CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID"));

            var entry = library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null) return Task.FromResult(CreateResponse(ResponseStatus.NotFound));

            try
            {
                library.MovePlaylistSong(entry.Index, entry.Index - 1, accessToken);
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
                await library.PauseSongAsync(accessToken);
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
                await library.PlayNextSongAsync(accessToken);
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
            var valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (!valid) return CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID");

            var entry = library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null) return CreateResponse(ResponseStatus.NotFound, "Playlist entry not found");

            try
            {
                await library.PlaySongAsync(entry.Index, accessToken);
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
                await library.PlayPreviousSongAsync(accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized);
            }

            return CreateResponse(ResponseStatus.Success);
        }

        private Task<ResponseInfo> PostRemovePlaylistSong(JToken parameters)
        {
            var songGuid = Guid.Parse(parameters["entryGuid"].ToString());

            var entry = library.CurrentPlaylist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null) return Task.FromResult(CreateResponse(ResponseStatus.NotFound, "Guid not found"));

            library.RemoveFromPlaylist(new[] { entry.Index }, accessToken);

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private async Task PushAccessPermission(AccessPermission accessPermission)
        {
            var content = JObject.FromObject(new
            {
                accessPermission
            });

            var message = CreatePushMessage(PushAction.UpdateAccessPermission, content);

            await SendMessage(message);
        }

        private Task PushCurrentPlaybackTime(TimeSpan currentPlaybackTime)
        {
            var content = JObject.FromObject(new
            {
                currentPlaybackTime
            });

            var message = CreatePushMessage(PushAction.UpdateCurrentPlaybackTime, content);

            return SendMessage(message);
        }

        private Task PushGuestSystemInfo(int? remainingVotes)
        {
            var guestSystemInfo = new GuestSystemInfo
            {
                IsEnabled = remainingVotes.HasValue
            };

            if (remainingVotes.HasValue) guestSystemInfo.RemainingVotes = remainingVotes.Value;

            var message = CreatePushMessage(PushAction.UpdateGuestSystemInfo, JObject.FromObject(guestSystemInfo));

            return SendMessage(message);
        }

        private async Task PushPlaybackState(AudioPlayerState state)
        {
            var content = JObject.FromObject(new
            {
                state
            });

            var message = CreatePushMessage(PushAction.UpdatePlaybackState, content);

            await SendMessage(message);
        }

        private async Task PushPlaylist(Playlist playlist, AudioPlayerState state)
        {
            var content = MobileHelper.SerializePlaylist(playlist, state,
                await library.CurrentPlaybackTime.FirstAsync(),
                await library.TotalTime.FirstAsync());

            var message = CreatePushMessage(PushAction.UpdateCurrentPlaylist, content);

            await SendMessage(message);
        }

        private async Task<ResponseInfo> QueueRemoteSong(JToken parameters)
        {
            var permission = await library.RemoteAccessControl.ObserveAccessPermission(accessToken).FirstAsync();

            if (permission == AccessPermission.Guest)
            {
                var remainingVotes = await library.RemoteAccessControl.ObserveRemainingVotes(accessToken).FirstAsync();

                if (remainingVotes == null)
                    return CreateResponse(ResponseStatus.NotSupported, "Voting isn't supported");

                if (remainingVotes == 0) return CreateResponse(ResponseStatus.Rejected, "Not enough votes left");
            }

            var transferInfo = parameters.ToObject<SongTransferInfo>();

            var data = songTransfers.FirstAsync(x => x.TransferId == transferInfo.TransferId).Select(x => x.Data);

            var song = MobileSong.Create(transferInfo.Metadata, data);

            if (permission == AccessPermission.Guest)
                library.AddGuestSongToPlaylist(song, accessToken);

            else
                library.AddSongsToPlaylist(new[] { song }, accessToken);

            return CreateResponse(ResponseStatus.Success);
        }

        private async Task SendMessage(NetworkMessage content)
        {
            byte[] message;
            using (MeasureHelper.Measure())
            {
                message = await NetworkHelpers.PackMessageAsync(content);
            }

            await gate.WaitAsync();

            try
            {
                await socket.GetStream().WriteAsync(message, 0, message.Length);
            }

            catch (Exception)
            {
                disconnected.OnNext(Unit.Default);
            }

            finally
            {
                gate.Release();
            }
        }

        private Task<ResponseInfo> SetCurrentTime(JToken parameters)
        {
            var time = parameters["time"].ToObject<TimeSpan>();

            try
            {
                library.SetCurrentTime(time, accessToken);
            }

            catch (AccessException)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.Unauthorized));
            }

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private void SetupPushNotifications()
        {
            library.WhenAnyValue(x => x.CurrentPlaylist).Skip(1)
                .Merge(library.WhenAnyValue(x => x.CurrentPlaylist)
                    .Select(x => x.Changed().Select(y => x))
                    .Switch())
                .Merge(library.WhenAnyValue(x => x.CurrentPlaylist)
                    .Select(x => x.WhenAnyValue(y => y.CurrentSongIndex).Skip(1).Select(y => x))
                    .Switch())
                .CombineLatest(library.PlaybackState, Tuple.Create)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => PushPlaylist(x.Item1, x.Item2))
                .DisposeWith(disposable);

            library.PlaybackState.Skip(1)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => PushPlaybackState(x))
                .DisposeWith(disposable);

            library.RemoteAccessControl.ObserveAccessPermission(accessToken)
                .Skip(1)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => PushAccessPermission(x))
                .DisposeWith(disposable);

            library.RemoteAccessControl.ObserveRemainingVotes(accessToken)
                .Skip(1)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => PushGuestSystemInfo(x))
                .DisposeWith(disposable);

            var lastTime = TimeSpan.Zero;
            // We can assume that, if the total time difference exceeds two seconds, the time change
            // is from an external source (e.g the user clicked on the time slider)
            library.CurrentPlaybackTime
                .Select(x =>
                    Math.Abs(lastTime.TotalSeconds - x.TotalSeconds) >= 2
                        ? Tuple.Create(x, true)
                        : Tuple.Create(x, false))
                .Do(x => lastTime = x.Item1)
                .Where(x => x.Item2)
                .Select(x => x.Item1)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(x => PushCurrentPlaybackTime(x))
                .DisposeWith(disposable);
        }

        private Task<ResponseInfo> SetVolume(JToken parameters)
        {
            var volume = parameters["volume"].ToObject<float>();

            if (volume < 0 || volume > 1.0)
                return Task.FromResult(
                    CreateResponse(ResponseStatus.MalformedRequest, "Volume must be between 0 and 1"));

            try
            {
                library.SetVolume(volume, accessToken);
            }

            catch (AccessException)
            {
                return Task.FromResult(CreateResponse(ResponseStatus.Unauthorized));
            }

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private Task<ResponseInfo> ToggleVideoPlayer(JToken arg)
        {
            videoPlayerToggleRequest.OnNext(Unit.Default);

            return Task.FromResult(CreateResponse(ResponseStatus.Success));
        }

        private bool TryValidateSongGuids(IEnumerable<string> guidStrings, out IEnumerable<Song> foundSongs,
            out ResponseInfo responseInfo)
        {
            var guids = new List<Guid>();

            foreach (var guidString in guidStrings)
            {
                Guid guid;

                var valid = Guid.TryParse(guidString, out guid);

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

            // Look if any song in our local library or any song of the last SoundCloud or YouTube
            // requests has the requested Guid
            var dic = library.Songs
                .Concat(lastSoundCloudRequest.Cast<Song>())
                .Concat(lastYoutubeRequest)
                .ToDictionary(x => x.Guid);

            var songs = guids.Select(x =>
                {
                    Song song;

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
            var remainingVotes = await library.RemoteAccessControl.ObserveRemainingVotes(accessToken).FirstAsync();

            if (remainingVotes == null) return CreateResponse(ResponseStatus.NotSupported, "Voting isn't supported");

            if (remainingVotes == 0) return CreateResponse(ResponseStatus.Rejected, "Not enough votes left");

            Guid songGuid;
            var valid = Guid.TryParse(parameters["entryGuid"].ToString(), out songGuid);

            if (!valid) return CreateResponse(ResponseStatus.MalformedRequest, "Malformed GUID");

            var playlist = library.CurrentPlaylist;
            var entry = playlist.FirstOrDefault(x => x.Guid == songGuid);

            if (entry == null) return CreateResponse(ResponseStatus.NotFound, "Playlist entry not found");

            if (library.RemoteAccessControl.IsVoteRegistered(accessToken, entry))
                return CreateResponse(ResponseStatus.Rejected, "Vote already registered");

            if (playlist.CurrentSongIndex.HasValue && entry.Index <= playlist.CurrentSongIndex.Value)
                return CreateResponse(ResponseStatus.Rejected, "Vote rejected");

            try
            {
                library.VoteForPlaylistEntry(entry.Index, accessToken);
            }

            catch (AccessException)
            {
                return CreateResponse(ResponseStatus.Unauthorized, "Unauthorized");
            }

            return CreateResponse(ResponseStatus.Success);
        }
    }
}