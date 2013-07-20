﻿using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

namespace Espera.Core.Tests
{
    internal static class Helpers
    {
        public static readonly LocalSong LocalSong1 = new LocalSong("C:/Music/Path1/Song1.mp3", AudioType.Mp3, TimeSpan.FromTicks(1), DriveType.Fixed)
        {
            Album = "Album1",
            Artist = "Artist1",
            Genre = "Genre1",
            Title = "Title1",
            TrackNumber = 1
        };

        public static readonly LocalSong LocalSong2 = new LocalSong("C:/Music/Path2/Song2.mp3", AudioType.Wav, TimeSpan.FromTicks(2), DriveType.Fixed)
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
            new YoutubeSong("www.youtube.com?watch=xyz", AudioType.Mp3, TimeSpan.FromTicks(1), true) { Title = "Title1" };

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
            return new Library(
                new Mock<IRemovableDriveWatcher>().Object,
                new Mock<ILibraryReader>().Object,
                writer ?? new Mock<ILibraryWriter>().Object,
                settings ?? new Mock<ILibrarySettings>().SetupAllProperties().Object,
                fileSystem ?? new MockFileSystem());
        }

        public static Library CreateLibraryWithPlaylist(string playlistName = "Playlist", ILibrarySettings settings = null)
        {
            var library = CreateLibrary(settings);
            library.AddAndSwitchToPlaylist(playlistName);

            return library;
        }

        public static Mock<Song> CreateSongMock(string name = "Song", bool callBase = false, AudioType audioType = AudioType.Mp3, TimeSpan duration = new TimeSpan())
        {
            return new Mock<Song>(name, audioType, duration) { CallBase = callBase };
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
                "  <Version>1.1.0</Version>" +
                "  <SongSourcePath>" + SongSourcePath + "</SongSourcePath>" +
                "  <Songs>" +
                "    <Song Album=\"" + LocalSong1.Album + "\" Artist=\"" + LocalSong1.Artist + "\" AudioType=\"" + LocalSong1.AudioType + "\" Duration=\"" + LocalSong1.Duration.Ticks + "\" Genre=\"" + LocalSong1.Genre + "\" Path=\"" + LocalSong1.OriginalPath + "\" Title=\"" + LocalSong1.Title + "\" TrackNumber=\"" + LocalSong1.TrackNumber + "\" />" +
                "    <Song Album=\"" + LocalSong2.Album + "\" Artist=\"" + LocalSong2.Artist + "\" AudioType=\"" + LocalSong2.AudioType + "\" Duration=\"" + LocalSong2.Duration.Ticks + "\" Genre=\"" + LocalSong2.Genre + "\" Path=\"" + LocalSong2.OriginalPath + "\" Title=\"" + LocalSong2.Title + "\" TrackNumber=\"" + LocalSong2.TrackNumber + "\" />" +
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

        public static Song SetupSongMock(string name = "Song", bool callBase = false, AudioType audioType = AudioType.Mp3, TimeSpan duration = new TimeSpan())
        {
            return CreateSongMock(name, callBase, audioType, duration).Object;
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