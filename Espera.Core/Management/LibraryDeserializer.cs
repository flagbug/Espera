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
                    var typeString = entry["type"].ToObject<string>();
                    Type type;

                    if (typeString == "Local")
                    {
                        type = typeof(LocalSong);
                    }

                    else if (typeString == "YouTube")
                    {
                        type = typeof(YoutubeSong);
                    }

                    else if (typeString == "SoundCloud")
                    {
                        type = typeof(SoundCloudSong);
                    }

                    else
                    {
                        throw new NotImplementedException("Type not implemented.");
                    }

                    TimeSpan? duration = null;
                    string title = null;

                    if (type == typeof(YoutubeSong) || type == typeof(SoundCloudSong))
                    {
                        duration = TimeSpan.FromTicks(entry["duration"].ToObject<long>());
                        title = entry["title"].ToObject<string>();
                    }

                    string artist = null;
                    string playbackPath = null;

                    if (type == typeof(SoundCloudSong))
                    {
                        artist = entry["artist"].ToObject<string>();
                        playbackPath = entry["playbackPath"].ToObject<string>();
                    }

                    return new
                    {
                        OriginalPath = entry["path"].ToObject<string>(),
                        PlaybackPath = playbackPath,
                        Type = type,
                        Duration = duration,
                        Title = title,
                        Artist = artist
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
                        return new YoutubeSong(entry.OriginalPath, entry.Duration.Value)
                        {
                            Title = entry.Title
                        };
                    }

                    if (entry.Type == typeof(SoundCloudSong))
                    {
                        return new SoundCloudSong(entry.OriginalPath, entry.PlaybackPath)
                        {
                            Artist = entry.Artist,
                            Title = entry.Title,
                            Duration = entry.Duration.Value
                        };
                    }

                    if (entry.Type == typeof(LocalSong))
                    {
                        // We search for the path locally, so we don't have to serialize data about
                        // local songs
                        return songs.First(song => song.OriginalPath == entry.OriginalPath);
                    }

                    throw new NotImplementedException();
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