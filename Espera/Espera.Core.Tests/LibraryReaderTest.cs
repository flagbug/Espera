using System;
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
            Song[] songs = LibraryReader.ReadSongs(Helpers.SaveFile.ToStream()).ToArray();

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

        [Test]
        public void ReadPlaylists()
        {
            Playlist[] playlists = LibraryReader.ReadPlaylists(Helpers.SaveFile.ToStream()).ToArray();

            Playlist playlist1 = playlists[0];
            Song[] songs1 = playlist1.ToArray();

            Assert.AreEqual("Playlist1", playlist1.Name);
            Assert.AreEqual("Path1", songs1[0].OriginalPath);
            Assert.IsInstanceOf(typeof(LocalSong), songs1[0]);
            Assert.AreEqual("Path2", songs1[1].OriginalPath);
            Assert.IsInstanceOf(typeof(LocalSong), songs1[1]);

            Playlist playlist2 = playlists[1];
            Song[] songs2 = playlist2.ToArray();

            Assert.AreEqual("Playlist2", playlist2.Name);
            Assert.AreEqual("Path1", songs2[0].OriginalPath);
            Assert.IsInstanceOf(typeof(LocalSong), songs2[0]);
            Assert.AreEqual("www.youtube.com?watch=xyz", songs2[1].OriginalPath);
            Assert.AreEqual(1, songs2[1].Duration.Ticks);
            Assert.IsInstanceOf(typeof(YoutubeSong), songs2[1]);
        }
    }
}