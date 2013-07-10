using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Espera.Core.Management
{
    internal static class LibraryWriter
    {
        public static void Write(IEnumerable<LocalSong> songs, IEnumerable<Playlist> playlists, string songSourcePath, Stream targetStream)
        {
            var document = new XDocument(
                new XElement("Root",
                    new XElement("Version", "2.0.0"),
                    new XElement("SongSourcePath", songSourcePath),
                    new XElement("Songs", songs.Select(song =>
                        new XElement("Song",
                            new XAttribute("Album", song.Album),
                            new XAttribute("Artist", song.Artist),
                            new XAttribute("AudioType", song.AudioType),
                            new XAttribute("Duration", song.Duration.Ticks),
                            new XAttribute("Genre", song.Genre),
                            new XAttribute("Path", song.OriginalPath),
                            new XAttribute("Title", song.Title),
                            new XAttribute("TrackNumber", song.TrackNumber)))),
                    new XElement("Playlists", playlists.Select(playlist =>
                        new XElement("Playlist",
                            new XAttribute("Name", playlist.Name),
                            new XElement("Entries", playlist.Select(entry =>
                                new XElement("Entry",
                                    new XAttribute("Path", entry.Song.OriginalPath),
                                    entry.Song is YoutubeSong ? new XAttribute("Title", entry.Song.Title) : null,
                                    new XAttribute("Type", (entry.Song is LocalSong) ? "Local" : "YouTube"),
                                    entry.Song is YoutubeSong ? new XAttribute("Duration", entry.Song.Duration.Ticks) : null))))))));

            document.Save(targetStream);
        }
    }
}