using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

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
                    artworkKey = song.ArtworkKey.FirstAsync().Wait()
                }),
                playlists = playlists.Select(playlist => new
                {
                    name = playlist.Name,
                    entries = playlist.Select(entry => new
                    {
                        path = entry.Song.OriginalPath,
                        title = entry.Song is YoutubeSong ? entry.Song.Title : null,
                        type = entry.Song is YoutubeSong ? "YouTube" : "Local",
                        duration = entry.Song is YoutubeSong ? new long?(entry.Song.Duration.Ticks) : null
                    })
                })
            }, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore });

            using (var sw = new StreamWriter(targetStream))
            using (var jw = new JsonTextWriter(sw))
            {
                jw.Formatting = Formatting.Indented;

                json.WriteTo(jw);
            }
        }
    }
}