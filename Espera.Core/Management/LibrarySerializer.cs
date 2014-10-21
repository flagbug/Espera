using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Espera.Core.Management
{
    internal static class LibrarySerializer
    {
        public static void Serialize(IEnumerable<LocalSong> songs, IEnumerable<Playlist> playlists, string songSourcePath, Stream targetStream)
        {
            var json = JObject.FromObject(new
            {
                version = "2.0.0",
                songSourcePath,
                songs = songs.Select(song => new
                {
                    album = song.Album,
                    artist = song.Artist,
                    duration = song.Duration.Ticks,
                    genre = song.Genre,
                    path = song.OriginalPath,
                    title = song.Title,
                    trackNumber = song.TrackNumber,
                    artworkKey = song.ArtworkKey
                }),
                playlists = playlists.Select(playlist => new
                {
                    name = playlist.Name,
                    entries = playlist.Select(entry => SerializeSong(entry.Song))
                })
            }, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore });

            using (var sw = new StreamWriter(targetStream, Encoding.UTF8, 64 * 1024, true))
            using (var jw = new JsonTextWriter(sw))
            {
                jw.Formatting = Formatting.Indented;

                json.WriteTo(jw);
            }
        }

        private static object SerializeSong(Song song)
        {
            // If the song is a local song, we only need to store the path and later look it up locally
            //
            // For SoundCloud songs, we need to store the artist (uploader) and playback path additionally
            string type;

            if (song is LocalSong)
            {
                type = "Local";
            }

            else if (song is YoutubeSong)
            {
                type = "YouTube";
            }

            else if (song is SoundCloudSong)
            {
                type = "SoundCloud";
            }

            else
            {
                throw new NotImplementedException("Song type not implemented.");
            }

            return new
            {
                path = song.OriginalPath,
                playbackPath = song is SoundCloudSong ? song.PlaybackPath : null,
                title = song is LocalSong ? null : song.Title,
                type,
                duration = song is LocalSong ? null : new long?(song.Duration.Ticks),
                artist = song is SoundCloudSong ? song.Artist : null
            };
        }
    }
}