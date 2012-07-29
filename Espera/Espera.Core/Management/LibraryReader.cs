using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Espera.Core.Audio;

namespace Espera.Core.Management
{
    internal class LibraryReader
    {
        public static IEnumerable<LocalSong> ReadSongs(Stream stream)
        {
            return XDocument.Load(stream)
                .Descendants("Root")
                .Descendants("Songs")
                .Elements("Song")
                .Select
                (
                    song =>
                        new LocalSong
                        (
                            song.Attribute("Path").Value,
                            (AudioType)Enum.Parse(typeof(AudioType), song.Attribute("AudioType").Value),
                            TimeSpan.FromTicks(Int64.Parse(song.Attribute("Duration").Value))
                        )
                        {
                            Album = song.Attribute("Album").Value,
                            Artist = song.Attribute("Artist").Value,
                            Genre = song.Attribute("Genre").Value,
                            Title = song.Attribute("Title").Value,
                            TrackNumber = Int32.Parse(song.Attribute("TrackNumber").Value)
                        }
                )
                .ToList();
        }

        public static IEnumerable<Playlist> ReadPlaylists(Stream stream)
        {
            IEnumerable<Song> songs = ReadSongs(stream);

            stream.Position = 0;

            var playlists = XDocument.Load(stream)
                .Descendants("Root")
                .Descendants("Playlists")
                .Elements("Playlist")
                .Select
                (
                    playlist =>
                        new
                        {
                            Name = playlist.Attribute("Name").Value,
                            Entries = playlist
                                .Descendants("Entries")
                                .Elements("Entry")
                                .Select
                                (
                                    entry =>
                                    {
                                        var type = entry.Attribute("Type").Value == "Local" ? typeof(LocalSong) : typeof(YoutubeSong);

                                        TimeSpan? duration = null;
                                        string title = null;

                                        if (type == typeof(YoutubeSong))
                                        {
                                            duration = TimeSpan.FromTicks(Int64.Parse(entry.Attribute("Duration").Value));
                                            title = entry.Attribute("Title").Value;
                                        }

                                        return new
                                        {
                                            Path = entry.Attribute("Path").Value,
                                            Type = type,
                                            Duration = duration,
                                            Title = title
                                        };
                                    }
                                )
                        }
                );

            return playlists.Select
            (
                p =>
                {
                    var playlist = new Playlist(p.Name);

                    var s = p.Entries
                        .Select
                        (
                            entry =>
                            {
                                if (entry.Type == typeof(YoutubeSong))
                                {
                                    return new YoutubeSong(entry.Path, AudioType.Mp3, entry.Duration.Value, CoreSettings.Default.StreamYoutube)
                                    {
                                        Title = entry.Title
                                    };
                                }

                                return songs.First(song => song.OriginalPath == entry.Path);
                            }
                        );

                    playlist.AddSongs(s);

                    return playlist;
                }
            )
            .ToList();
        }
    }
}