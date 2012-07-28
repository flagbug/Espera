using System.IO;

namespace Espera.Core.Tests
{
    internal static class Helpers
    {
        public static string SaveFile =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Root>" +
            "   <Version>1.0.0</Version>" +
            "   <Songs>" +
            "       <Song Album=\"Album1\" Artist=\"Artist1\" AudioType=\"Mp3\" Duration=\"1\" Genre=\"Genre1\" Path=\"Path1\" Title=\"Title1\" TrackNumber=\"1\" />" +
            "       <Song Album=\"Album2\" Artist=\"Artist2\" AudioType=\"Wav\" Duration=\"2\" Genre=\"Genre2\" Path=\"Path2\" Title=\"Title2\" TrackNumber=\"2\" />" +
            "   </Songs>" +
            "   <Playlists>" +
            "       <Playlist Name=\"Playlist1\">" +
            "           <Entries>" +
            "               <Entry Path=\"Path1\" Type=\"Local\" />" +
            "               <Entry Path=\"Path2\" Type=\"Local\" />" +
            "           </Entries>" +
            "       </Playlist>" +
            "       <Playlist Name=\"Playlist2\">" +
            "           <Entries>" +
            "               <Entry Path=\"Path1\" Type=\"Local\" />" +
            "               <Entry Path=\"www.youtube.com?watch=xyz\" Type=\"YouTube\" Duration=\"1\" />" +
            "           </Entries>" +
            "       </Playlist>" +
            "   </Playlists>" +
            "</Root>";

        public static Stream ToStream(this string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}