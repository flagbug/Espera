using System.Linq;
using System.Xml.Linq;

namespace Espera.Core.Management
{
    internal class LibraryWriter
    {
        public static void Write(Library library)
        {
            var document = new XDocument(
                new XElement("Root",
                    new XElement("Version", "1.0.0"),
                    new XElement("Songs", library.Songs.Select(song =>
                        new XElement("Song",
                            new XAttribute("Album", song.Album),
                            new XAttribute("Artist", song.Artist),
                            new XAttribute("AudioType", song.AudioType),
                            new XAttribute("Duration", song.Duration.Ticks),
                            new XAttribute("Genre", song.Genre),
                            new XAttribute("Path", song.OriginalPath),
                            new XAttribute("Title", song.Title),
                            new XAttribute("TrackNumber", song.TrackNumber)))),
                    new XElement("Playlists", library.Playlists.Select(playlist =>
                        new XElement("Playlist",
                            new XAttribute("Name", playlist.Name),
                            new XElement("Songs", playlist.Songs.Select(song =>
                                new XElement("Path", song.OriginalPath))))))));

            document.Save("library.xml");
        }
    }
}