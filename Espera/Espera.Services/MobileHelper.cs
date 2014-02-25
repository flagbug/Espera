using Espera.Core;
using Espera.Core.Management;
using Espera.Network;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Espera.Services
{
    public static class MobileHelper
    {
        public static JObject SerializePlaylist(Playlist playlist, int? remainingVotes)
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
                ).ToList(),
                RemainingVotes = remainingVotes
            };

            return new JObject(networkPlaylist);
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

            return new JObject(networkSongs);
        }
    }
}