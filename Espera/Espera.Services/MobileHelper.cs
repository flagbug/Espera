using Espera.Core;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Network;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Espera.Services
{
    public static class MobileHelper
    {
        public static JObject SerializePlaylist(Playlist playlist, int? remainingVotes, AudioPlayerState playbackState)
        {
            var networkPlaylist = new NetworkPlaylist
            {
                Name = playlist.Name,
                CurrentIndex = playlist.CurrentSongIndex.Value,
                Songs = playlist.Select(x =>

                    new NetworkSong
                    {
                        Artist = x.Song.Artist,
                        Title = x.Song.Title,
                        Source = x.Song is LocalSong ? NetworkSongSource.Local : NetworkSongSource.Youtube,
                        Guid = x.Guid
                    }
                ).ToList().AsReadOnly(),
                RemainingVotes = remainingVotes,
                PlaybackState = (NetworkPlaybackState)Enum.ToObject(typeof(NetworkPlaybackState), (int)playbackState)
            };

            return JObject.FromObject(networkPlaylist);
        }

        public static JObject SerializeSongs(IEnumerable<LocalSong> songs)
        {
            var networkSongs = songs.Select(x =>
                new NetworkSong
                {
                    Album = x.Album,
                    Artist = x.Artist,
                    Duration = x.Duration,
                    Genre = x.Genre,
                    Title = x.Title,
                    Guid = x.Guid
                });

            var serialized = JObject.FromObject(new
            {
                songs = networkSongs
            });

            return JObject.FromObject(serialized);
        }
    }
}