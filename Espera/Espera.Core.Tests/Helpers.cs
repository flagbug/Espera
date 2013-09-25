﻿using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Espera.Core.Tests
{
    internal static class Helpers
    {
        public static readonly LocalSong LocalSong1 = new LocalSong("C:/Music/Path1/Song1.mp3", TimeSpan.FromTicks(1), DriveType.Fixed, "artwork-7e316d0e701df0505fa72e2b89467910")
        {
            Album = "Album1",
            Artist = "Artist1",
            Genre = "Genre1",
            Title = "Title1",
            TrackNumber = 1
        };

        public static readonly LocalSong LocalSong2 = new LocalSong("C:/Music/Path2/Song2.mp3", TimeSpan.FromTicks(2), DriveType.Fixed, null)
        {
            Album = "Album2",
            Artist = "Artist2",
            Genre = "Genre2",
            Title = "Title2",
            TrackNumber = 2
        };

        public static readonly Playlist Playlist1;

        public static readonly Playlist Playlist2;

        public static readonly string SongSourcePath = "C:/Music/";

        public static readonly YoutubeSong YoutubeSong1 =
            new YoutubeSong("www.youtube.com?watch=xyz", TimeSpan.FromTicks(1), true) { Title = "Title1" };

        static Helpers()
        {
            Playlist1 = new Playlist("Playlist1");
            Playlist1.AddSongs(new[] { LocalSong1, LocalSong2 });

            Playlist2 = new Playlist("Playlist2");
            Playlist2.AddSongs(new[] { (Song)LocalSong1, YoutubeSong1 });
        }

        public static Library CreateLibrary(ILibraryWriter writer)
        {
            return CreateLibrary(null, writer);
        }

        public static Library CreateLibrary(ILibrarySettings settings = null, ILibraryWriter writer = null, MockFileSystem fileSystem = null)
        {
            var library = new Library(
                new Mock<IRemovableDriveWatcher>().Object,
                new Mock<ILibraryReader>().Object,
                writer ?? new Mock<ILibraryWriter>().Object,
                settings ?? new Mock<ILibrarySettings>().SetupAllProperties().Object,
                fileSystem ?? new MockFileSystem());

            IAudioPlayerCallback c = library.AudioPlayerCallback;
            c.GetTime = () => TimeSpan.Zero;
            c.GetVolume = () => 1.0f;
            c.LoadRequest = () => { };
            c.PauseRequest = () => { };
            c.PlayRequest = c.Finished;
            c.SetTime = x => { };
            c.SetVolume = x => { };
            c.StopRequest = () => { };

            return library;
        }

        public static Library CreateLibraryWithPlaylist(string playlistName = "Playlist", ILibrarySettings settings = null)
        {
            var library = CreateLibrary(settings);
            library.AddAndSwitchToPlaylist(playlistName);

            return library;
        }

        public static Mock<Song> CreateSongMock(string name = "Song", bool callBase = false, TimeSpan duration = new TimeSpan())
        {
            var mock = new Mock<Song>(name, duration) { CallBase = callBase };
            mock.Setup(x => x.PrepareAsync()).Returns(Task.Delay(0));

            return mock;
        }

        public static Mock<Song>[] CreateSongMocks(int count, bool callBase)
        {
            var songs = new Mock<Song>[count];

            for (int i = 0; i < count; i++)
            {
                songs[i] = CreateSongMock("Song" + i, callBase);
            }

            return songs;
        }

        public static string GenerateSaveFile()
        {
            return
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Root>" +
                "  <Version>2.0.0</Version>" +
                "  <SongSourcePath>" + SongSourcePath + "</SongSourcePath>" +
                "  <Songs>" +
                "    <Song Album=\"" + LocalSong1.Album + "\" Artist=\"" + LocalSong1.Artist + "\" Duration=\"" + LocalSong1.Duration.Ticks + "\" Genre=\"" + LocalSong1.Genre + "\" Path=\"" + LocalSong1.OriginalPath + "\" Title=\"" + LocalSong1.Title + "\" TrackNumber=\"" + LocalSong1.TrackNumber + "\" ArtworkKey=\"" + (LocalSong1.ArtworkKey.FirstAsync().Wait() ?? String.Empty) + "\" />" +
                "    <Song Album=\"" + LocalSong2.Album + "\" Artist=\"" + LocalSong2.Artist + "\" Duration=\"" + LocalSong2.Duration.Ticks + "\" Genre=\"" + LocalSong2.Genre + "\" Path=\"" + LocalSong2.OriginalPath + "\" Title=\"" + LocalSong2.Title + "\" TrackNumber=\"" + LocalSong2.TrackNumber + "\" ArtworkKey=\"" + (LocalSong2.ArtworkKey.FirstAsync().Wait() ?? String.Empty) + "\" />" +
                "  </Songs>" +
                "  <Playlists>" +
                "    <Playlist Name=\"" + Playlist1.Name + "\">" +
                "      <Entries>" +
                "        <Entry Path=\"" + LocalSong1.OriginalPath + "\" Type=\"Local\" />" +
                "        <Entry Path=\"" + LocalSong2.OriginalPath + "\" Type=\"Local\" />" +
                "      </Entries>" +
                "    </Playlist>" +
                "    <Playlist Name=\"" + Playlist2.Name + "\">" +
                "      <Entries>" +
                "        <Entry Path=\"" + LocalSong1.OriginalPath + "\" Type=\"Local\" />" +
                "        <Entry Path=\"" + YoutubeSong1.OriginalPath + "\" Title=\"" + YoutubeSong1.Title + "\" Type=\"YouTube\" Duration=\"" + YoutubeSong1.Duration.Ticks + "\" />" +
                "      </Entries>" +
                "    </Playlist>" +
                "  </Playlists>" +
                "</Root>";
        }

        public static Song SetupSongMock(string name = "Song", bool callBase = false, TimeSpan duration = new TimeSpan())
        {
            return CreateSongMock(name, callBase, duration).Object;
        }

        public static Song[] SetupSongMocks(int count, bool callBase = false)
        {
            var songs = new Song[count];

            for (int i = 0; i < count; i++)
            {
                songs[i] = SetupSongMock("Song" + i, callBase);
            }

            return songs;
        }

        public static string StreamToString(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                stream.Position = 0;

                return reader.ReadToEnd();
            }
        }

        public static Stream ToStream(this string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        internal static Playlist SetupPlaylist(Song song)
        {
            return SetupPlaylist(new[] { song });
        }

        internal static Playlist SetupPlaylist(IEnumerable<Song> songs)
        {
            var playlist = new Playlist("Test Playlist");

            playlist.AddSongs(songs);

            return playlist;
        }
    }
}