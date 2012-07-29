using System;
using System.IO;
using Espera.Core.Management;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class LibraryWriterTest
    {
        [Test]
        public void Write()
        {
            using (Stream targetStream = new MemoryStream())
            {
                var songs = new[] { Helpers.LocalSong1, Helpers.LocalSong2 };

                var playlists = new[] { new PlaylistInfo(Helpers.Playlist1), new PlaylistInfo(Helpers.Playlist2) };

                LibraryWriter.Write(songs, playlists, targetStream);

                string expected = Helpers.GenerateSaveFile();
                string actual = StreamToString(targetStream).Replace("\r\n", String.Empty);

                Assert.AreEqual(expected, actual);
            }
        }

        private static string StreamToString(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                stream.Position = 0;

                return reader.ReadToEnd();
            }
        }
    }
}