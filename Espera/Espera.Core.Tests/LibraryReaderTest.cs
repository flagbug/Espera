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
                Playlist[] playlists = LibraryReader.ReadPlaylists(saveFileStream).ToArray();

                Playlist playlist1 = playlists[0];
                Song[] songs1 = playlist1.ToArray();
                Song localSong1 = Helpers.LocalSong1;
                Song localSong2 = Helpers.LocalSong2;

                Assert.AreEqual("Playlist1", playlist1.Name);
                Assert.AreEqual(localSong1.OriginalPath, songs1[0].OriginalPath);
                Assert.IsInstanceOf(localSong1.GetType(), songs1[0]);

                Assert.AreEqual(localSong2.OriginalPath, songs1[1].OriginalPath);
                Assert.IsInstanceOf(localSong2.GetType(), songs1[1]);

                Playlist playlist2 = playlists[1];
                Song[] songs2 = playlist2.ToArray();
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
                LocalSong[] songs = LibraryReader.ReadSongs(saveFileStream).ToArray();

                Song actualSong1 = songs[0];
                Song excpectedSong1 = Helpers.LocalSong1;

                Assert.AreEqual(excpectedSong1.Album, actualSong1.Album);
                Assert.AreEqual(excpectedSong1.Artist, actualSong1.Artist);
                Assert.AreEqual(excpectedSong1.AudioType, actualSong1.AudioType);
                Assert.AreEqual(excpectedSong1.Duration, actualSong1.Duration);
                Assert.AreEqual(excpectedSong1.Genre, actualSong1.Genre);
                Assert.AreEqual(excpectedSong1.OriginalPath, actualSong1.OriginalPath);
                Assert.AreEqual(excpectedSong1.Title, actualSong1.Title);
                Assert.AreEqual(excpectedSong1.TrackNumber, actualSong1.TrackNumber);

                Song actualSong2 = songs[1];
                Song expectedSong2 = Helpers.LocalSong2;

                Assert.AreEqual(expectedSong2.Album, actualSong2.Album);
                Assert.AreEqual(expectedSong2.Artist, actualSong2.Artist);
                Assert.AreEqual(expectedSong2.AudioType, actualSong2.AudioType);
                Assert.AreEqual(expectedSong2.Duration, actualSong2.Duration);
                Assert.AreEqual(expectedSong2.Genre, actualSong2.Genre);
                Assert.AreEqual(expectedSong2.OriginalPath, actualSong2.OriginalPath);
                Assert.AreEqual(expectedSong2.Title, actualSong2.Title);
                Assert.AreEqual(expectedSong2.TrackNumber, actualSong2.TrackNumber);
            }
        }
    }
}