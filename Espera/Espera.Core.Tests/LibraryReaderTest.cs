using System;
using System.IO;
using System.Linq;
using Espera.Core.Audio;
using Espera.Core.Management;
using NUnit.Framework;

namespace Espera.Core.Tests
{
    [TestFixture]
    public class LibraryReaderTest
    {
        [Test]
        public void ReadSongs()
        {
            using (Stream saveFileStream = Helpers.GenerateSaveFile().ToStream())
            {
                Song[] songs = LibraryReader.ReadSongs(saveFileStream).ToArray();

                Song song1 = songs[0];

                Assert.AreEqual("Album1", song1.Album);
                Assert.AreEqual("Artist1", song1.Artist);
                Assert.AreEqual(AudioType.Mp3, song1.AudioType);
                Assert.AreEqual(TimeSpan.FromTicks(1), song1.Duration);
                Assert.AreEqual("Genre1", song1.Genre);
                Assert.AreEqual("Path1", song1.OriginalPath);
                Assert.AreEqual("Title1", song1.Title);
                Assert.AreEqual(1, song1.TrackNumber);

                Song song2 = songs[1];

                Assert.AreEqual("Album2", song2.Album);
                Assert.AreEqual("Artist2", song2.Artist);
                Assert.AreEqual(AudioType.Wav, song2.AudioType);
                Assert.AreEqual(TimeSpan.FromTicks(2), song2.Duration);
                Assert.AreEqual("Genre2", song2.Genre);
                Assert.AreEqual("Path2", song2.OriginalPath);
                Assert.AreEqual("Title2", song2.Title);
                Assert.AreEqual(2, song2.TrackNumber);
            }
        }

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
    }
}