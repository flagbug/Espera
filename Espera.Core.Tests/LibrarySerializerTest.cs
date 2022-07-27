using System.IO;
using Espera.Core.Management;
using Xunit;

namespace Espera.Core.Tests
{
    public class LibrarySerializerTest
    {
        public class TheWriteMethod
        {
            [Fact(Skip = "Json does wierd things")]
            public void SmokeTest()
            {
                using (Stream targetStream = new MemoryStream())
                {
                    var songs = new[] { Helpers.LocalSong1, Helpers.LocalSong2 };

                    var playlists = new[] { Helpers.Playlist1, Helpers.Playlist2 };

                    LibrarySerializer.Serialize(songs, playlists, Helpers.SongSourcePath, targetStream);

                    var expected = Helpers.GenerateSaveFile();
                    var actual = Helpers.StreamToString(targetStream).Replace("\r\n", string.Empty);

                    Assert.Equal(expected, actual);
                }
            }
        }
    }
}