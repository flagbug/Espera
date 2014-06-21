using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Network;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Espera.Core.Mobile
{
    public static class MobileHelper
    {
        public static JObject SerializePlaylist(Playlist playlist, int? remainingVotes, AudioPlayerState playbackState)
        {
            var networkPlaylist = new NetworkPlaylist
            {
                Name = playlist.Name,
                CurrentIndex = playlist.CurrentSongIndex,
                Songs = playlist.Select(x => x.Song.ToNetworkSong(x.Guid)).ToList().AsReadOnly(),
                RemainingVotes = remainingVotes,
                PlaybackState = (NetworkPlaybackState)Enum.ToObject(typeof(NetworkPlaybackState), (int)playbackState)
            };

            return JObject.FromObject(networkPlaylist);
        }

        public static JObject SerializeSongs(IEnumerable<LocalSong> songs)
        {
            var serialized = JObject.FromObject(new
            {
                songs = songs.Select(x => x.ToNetworkSong(x.Guid))
            });

            return JObject.FromObject(serialized);
        }

        private static NetworkSong ToNetworkSong(this Song song, Guid guid)
        {
            return new NetworkSong
            {
                Album = song.Album,
                Artist = song.Artist,
                Duration = song.Duration,
                Genre = song.Genre,
                Title = song.Title,
                Source = song.NetworkSongSource,
                Guid = guid
            };
        }
    }
}