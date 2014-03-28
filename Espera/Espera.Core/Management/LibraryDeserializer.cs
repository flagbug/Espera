using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Espera.Core.Management
{
    internal static class LibraryDeserializer
    {
        public static IReadOnlyList<Playlist> DeserializePlaylists(JObject json, IReadOnlyList<Song> songCache = null)
        {
            IEnumerable<Song> songs = songCache ?? DeserializeSongs(json);

            var playlists = json["playlists"].Select(playlist => new
            {
                Name = playlist["name"].ToObject<string>(),
                Entries = playlist["entries"].Select(entry =>
                {
                    var type = entry["type"].ToObject<string>() == "Local" ? typeof(LocalSong) : typeof(YoutubeSong);

                    TimeSpan? duration = null;
                    string title = null;

                    if (type == typeof(YoutubeSong))
                    {
                        duration = TimeSpan.FromTicks(entry["duration"].ToObject<long>());
                        title = entry["title"].ToObject<string>();
                    }

                    return new
                    {
                        Path = entry["path"].ToObject<string>(),
                        Type = type,
                        Duration = duration,
                        Title = title
                    };
                })
            });

            return playlists.Select(p =>
            {
                var playlist = new Playlist(p.Name);

                var s = p.Entries.Select(entry =>
                {
                    if (entry.Type == typeof(YoutubeSong))
                    {
                        return new YoutubeSong(entry.Path, entry.Duration.Value)
                        {
                            Title = entry.Title
                        };
                    }

                    return songs.First(song => song.OriginalPath == entry.Path);
                });

                playlist.AddSongs(s);

                return playlist;
            }).ToList();
        }

        public static IReadOnlyList<LocalSong> DeserializeSongs(JObject json)
        {
            return json["songs"].Select(song =>
                new LocalSong
                (
                    song["path"].ToObject<string>(),
                    TimeSpan.FromTicks(song["duration"].ToObject<long>()),
                    song["artworkKey"] == null ? null : song["artworkKey"].ToObject<string>()
                )
                {
                    Album = song["album"].ToObject<string>(),
                    Artist = song["artist"].ToObject<string>(),
                    Genre = song["genre"].ToObject<string>(),
                    Title = song["title"].ToObject<string>(),
                    TrackNumber = song["trackNumber"].ToObject<int>()
                }
            ).ToList();
        }

        public static string DeserializeSongSourcePath(JObject json)
        {
            return json["songSourcePath"] == null ? null : json["songSourcePath"].ToObject<string>();
        }
    }
}