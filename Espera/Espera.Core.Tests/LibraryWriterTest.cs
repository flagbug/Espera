using Espera.Core.Management;
using System;
using System.IO;
using Xunit;

namespace Espera.Core.Tests
{
    public class LibraryWriterTest
    {
        [Fact]
        public void WriteSmokeTest()
        {
            using (Stream targetStream = new MemoryStream())
            {
                var songs = new[] { Helpers.LocalSong1, Helpers.LocalSong2 };

                var playlists = new[] { Helpers.Playlist1, Helpers.Playlist2 };

                LibraryWriter.Write(songs, playlists, Helpers.SongSourcePath, targetStream);

                string expected = Helpers.GenerateSaveFile();
                string actual = Helpers.StreamToString(targetStream).Replace("\r\n", String.Empty);

                Assert.Equal(expected, actual);
            }
        }
    }
}