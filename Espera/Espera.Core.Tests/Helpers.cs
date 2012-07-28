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
            "       <Playlist Name=\"New Playlist 1\">" +
            "           <Entries>" +
            "               <Entry Path=\"PlaylistPath1\" Type=\"Local\" />" +
            "               <Entry Path=\"PlaylistPath2\" Type=\"YouTube\" />" +
            "           </Entries>" +
            "       </Playlist>" +
            "       <Playlist Name=\"New Playlist 2\">" +
            "           <Entries>" +
            "               <Entry Path=\"PlaylistPath1\" Type=\"Local\" />" +
            "               <Entry Path=\"PlaylistPath3\" Type=\"YouTube\" />" +
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