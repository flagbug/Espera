using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Espera.Core.Management
{
    internal class LibraryWriter
    {
        public static void Write(IEnumerable<LocalSong> songs, IEnumerable<PlaylistInfo> playlists, Stream targetStream)
        {
            var document = new XDocument(
                new XElement("Root",
                    new XElement("Version", "1.0.0"),
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
                            new XElement("Songs", playlist.Songs.Select(song =>
                                new XElement("Path", song.OriginalPath))))))));

            document.Save(targetStream);
        }
    }
}