using Espera.Core.Management;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class LibraryReaderTest
    {
        [Test]
        public void ReadPlaylists()
        {
            using (Stream saveFileStream = Helpers.GenerateSaveFile().ToStream())
            {
                Playlist[] playlists = LibraryReader.ReadPlaylists(saveFileStream, x => DriveType.Fixed).ToArray();

                Playlist playlist1 = playlists[0];
                Song[] songs1 = playlist1.Select(entry => entry.Song).ToArray();
                Song localSong1 = Helpers.LocalSong1;
                Song localSong2 = Helpers.LocalSong2;

                Assert.AreEqual("Playlist1", playlist1.Name);
                Assert.AreEqual(localSong1.OriginalPath, songs1[0].OriginalPath);
                Assert.IsInstanceOf(localSong1.GetType(), songs1[0]);

                Assert.AreEqual(localSong2.OriginalPath, songs1[1].OriginalPath);
                Assert.IsInstanceOf(localSong2.GetType(), songs1[1]);

                Playlist playlist2 = playlists[1];
                Song[] songs2 = playlist2.Select(entry => entry.Song).ToArray();
                Song youtubeSong1 = Helpers.YoutubeSong1;

                Assert.AreEqual("Playlist2", playlist2.Name);
                Assert.AreEqual(localSong1.OriginalPath, songs2[0].OriginalPath);
                Assert.IsInstanceOf(localSong1.GetType(), songs2[0]);

                Assert.AreEqual(youtubeSong1.OriginalPath, songs2[1].OriginalPath);
                Assert.AreEqual(youtubeSong1.Title, songs2[1].Title);
                Assert.AreEqual(youtubeSong1.Duration.Ticks, songs2[1].Duration.Ticks);
                Assert.IsInstanceOf(youtubeSong1.GetType(), songs2[1]);
            }
        }

        [Test]
        public void ReadSongs()
        {
            using (Stream saveFileStream = Helpers.GenerateSaveFile().ToStream())
            {
                LocalSong[] songs = LibraryReader.ReadSongs(saveFileStream, x => DriveType.Fixed).ToArray();

                Song actualSong1 = songs[0];
                Song expectedSong1 = Helpers.LocalSong1;

                AssertSongsAreEqual(expectedSong1, actualSong1);

                Song actualSong2 = songs[1];
                Song expectedSong2 = Helpers.LocalSong2;

                AssertSongsAreEqual(expectedSong2, actualSong2);
            }
        }

        private static void AssertSongsAreEqual(Song song1, Song song2)
        {
            Assert.AreEqual(song1.Album, song2.Album);
            Assert.AreEqual(song1.Artist, song2.Artist);
            Assert.AreEqual(song1.AudioType, song2.AudioType);
            Assert.AreEqual(song1.Duration, song2.Duration);
            Assert.AreEqual(song1.Genre, song2.Genre);
            Assert.AreEqual(song1.OriginalPath, song2.OriginalPath);
            Assert.AreEqual(song1.Title, song2.Title);
            Assert.AreEqual(song1.TrackNumber, song2.TrackNumber);
        }
    }
}