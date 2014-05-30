using System.Linq;
using Espera.Core.Management;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Espera.Core.Tests
{
    public class LibraryDeserializerTest
    {
        private static void AssertSongsAreEqual(Song song1, Song song2)
        {
            Assert.Equal(song1.Album, song2.Album);
            Assert.Equal(song1.Artist, song2.Artist);
            Assert.Equal(song1.Duration, song2.Duration);
            Assert.Equal(song1.Genre, song2.Genre);
            Assert.Equal(song1.OriginalPath, song2.OriginalPath);
            Assert.Equal(song1.Title, song2.Title);
            Assert.Equal(song1.TrackNumber, song2.TrackNumber);
        }

        public class TheReadPlaylistsMethod
        {
            [Fact(Skip = "Json does wierd things")]
            public void SmokeTest()
            {
                string json = Helpers.GenerateSaveFile();
                var jobject = JObject.Parse(json);

                Playlist[] playlists = LibraryDeserializer.DeserializePlaylists(jobject).ToArray();

                Playlist playlist1 = playlists[0];
                Song[] songs1 = playlist1.Select(entry => entry.Song).ToArray();
                Song localSong1 = Helpers.LocalSong1;
                Song localSong2 = Helpers.LocalSong2;

                Assert.Equal("Playlist1", playlist1.Name);
                Assert.Equal(localSong1.OriginalPath, songs1[0].OriginalPath);
                Assert.IsType<LocalSong>(songs1[0]);

                Assert.Equal(localSong2.OriginalPath, songs1[1].OriginalPath);
                Assert.IsType<LocalSong>(songs1[1]);

                Playlist playlist2 = playlists[1];
                Song[] songs2 = playlist2.Select(entry => entry.Song).ToArray();
                Song youtubeSong1 = Helpers.YoutubeSong1;

                Assert.Equal("Playlist2", playlist2.Name);
                AssertSongsAreEqual(songs2[0], Helpers.LocalSong1);

                Assert.Equal(youtubeSong1.OriginalPath, songs2[1].OriginalPath);
                Assert.Equal(youtubeSong1.Title, songs2[1].Title);
                Assert.Equal(youtubeSong1.Duration.Ticks, songs2[1].Duration.Ticks);
                Assert.IsType<YoutubeSong>(songs2[1]);
            }
        }

        public class TheReadSongsMethod
        {
            [Fact(Skip = "Json does wierd things")]
            public void SmokeTest()
            {
                string json = Helpers.GenerateSaveFile();
                var jobject = JObject.Parse(json);

                LocalSong[] songs = LibraryDeserializer.DeserializeSongs(jobject).ToArray();

                Song actualSong1 = songs[0];
                Song expectedSong1 = Helpers.LocalSong1;

                AssertSongsAreEqual(expectedSong1, actualSong1);

                Song actualSong2 = songs[1];
                Song expectedSong2 = Helpers.LocalSong2;

                AssertSongsAreEqual(expectedSong2, actualSong2);
            }
        }

        public class TheReadSongSourcePathMethod
        {
            [Fact(Skip = "Json does wierd things")]
            public void SmokeTest()
            {
                string json = Helpers.GenerateSaveFile();
                var jobject = JObject.Parse(json);

                string songSourcePath = LibraryDeserializer.DeserializeSongSourcePath(jobject);

                Assert.Equal(songSourcePath, Helpers.SongSourcePath);
            }
        }
    }
}