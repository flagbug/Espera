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
        public static JObject SerializePlaylist(Playlist playlist, AudioPlayerState playbackState, TimeSpan currentTime, TimeSpan totalTime)
        {
            var networkPlaylist = new NetworkPlaylist
            {
                Name = playlist.Name,
                CurrentIndex = playlist.CurrentSongIndex,
                Songs = playlist.Select(x => x.Song.ToNetworkSong(x.Guid)).ToList().AsReadOnly(),
                PlaybackState = (NetworkPlaybackState)Enum.ToObject(typeof(NetworkPlaybackState), (int)playbackState),
                CurrentTime = currentTime,
                TotalTime = totalTime
            };

            return JObject.FromObject(networkPlaylist);
        }

        public static JObject SerializeSongs(IEnumerable<Song> songs)
        {
            var serialized = JObject.FromObject(new
            {
                songs = songs.Select(x => x.ToNetworkSong(x.Guid))
            });

            return JObject.FromObject(serialized);
        }

        private static NetworkSong ToNetworkSong(this Song song, Guid guid)
        {
            string artworkKey = null;
            int playbackCount = 0;

            var soundCloudSong = song as SoundCloudSong;
            if (soundCloudSong != null && soundCloudSong.ArtworkUrl != null)
            {
                artworkKey = soundCloudSong.ArtworkUrl.ToString();
                playbackCount = soundCloudSong.PlaybackCount.GetValueOrDefault();
            }

            var youtubeSong = song as YoutubeSong;
            if (youtubeSong != null && youtubeSong.ThumbnailSource != null)
            {
                artworkKey = youtubeSong.ThumbnailSource.ToString();
                playbackCount = youtubeSong.Views;
            }

            return new NetworkSong
            {
                Album = song.Album,
                Artist = song.Artist,
                ArtworkKey = artworkKey,
                Duration = song.Duration,
                Genre = song.Genre,
                Title = song.Title,
                TrackNumber = song.TrackNumber,
                Source = song.NetworkSongSource,
                Guid = guid,
                PlaybackCount = playbackCount
            };
        }
    }
}