using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public static class Helpers
    {
        public static readonly LocalSong LocalSong1 = new LocalSong("C:/Music/Path1/Song1.mp3", TimeSpan.FromTicks(1), "artwork-7e316d0e701df0505fa72e2b89467910")
        {
            Album = "Album1",
            Artist = "Artist1",
            Genre = "Genre1",
            Title = "Title1",
            TrackNumber = 1
        };

        public static readonly LocalSong LocalSong2 = new LocalSong("C:/Music/Path2/Song2.mp3", TimeSpan.FromTicks(2))
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
            new YoutubeSong("www.youtube.com?watch=xyz", TimeSpan.FromTicks(1)) { Title = "Title1" };

        static Helpers()
        {
            Playlist1 = new Playlist("Playlist1");
            Playlist1.AddSongs(new[] { LocalSong1, LocalSong2 });

            Playlist2 = new Playlist("Playlist2");
            Playlist2.AddSongs(new[] { (Song)LocalSong1, YoutubeSong1 });
        }

        public static async Task AwaitInitializationAndUpdate(this Library library)
        {
            var updateCompleted = library.IsUpdating.Where(x => !x).Skip(1).FirstAsync().ToTask();

            library.Initialize();

            await updateCompleted;
        }

        public static Library CreateLibrary(ILibraryWriter writer)
        {
            return CreateLibrary(null, null, writer);
        }

        public static Library CreateLibrary(IFileSystem fileSystem)
        {
            return CreateLibrary(null, null, null, fileSystem);
        }

        public static Library CreateLibrary(ILibraryReader reader, IFileSystem fileSystem = null)
        {
            return CreateLibrary(null, reader, null, fileSystem);
        }

        public static Library CreateLibrary(IFileSystem fileSystem, ILocalSongFinder localSongFinder)
        {
            return CreateLibrary(null, null, null, fileSystem, localSongFinder);
        }

        public static Library CreateLibrary(ILibraryReader reader, IFileSystem fileSystem, ILocalSongFinder localSongFinder)
        {
            return CreateLibrary(null, reader, null, fileSystem, localSongFinder);
        }

        public static Library CreateLibrary(CoreSettings settings = null, ILibraryReader reader = null, ILibraryWriter writer = null,
            IFileSystem fileSystem = null, ILocalSongFinder localSongFinder = null)
        {
            var library = new Library(
                reader ?? Substitute.For<ILibraryReader>(),
                writer ?? Substitute.For<ILibraryWriter>(),
                settings ?? new CoreSettings(),
                fileSystem ?? new MockFileSystem(),
                x => localSongFinder ?? SetupDefaultLocalSongFinder());

            IAudioPlayerCallback c = library.AudioPlayerCallback;
            c.GetTime = () => TimeSpan.Zero;
            c.GetVolume = () => 1.0f;
            c.LoadRequest = () => Task.Delay(0);
            c.PauseRequest = () => Task.Delay(0);
            c.PlayRequest = () =>
            {
                Task.Run(() => library.AudioPlayerCallback.Finished());

                return Task.Delay(0);
            };
            c.SetTime = x => { };
            c.SetVolume = x => { };
            c.StopRequest = () => Task.Delay(0);

            return library;
        }

        public static Library CreateLibraryWithPlaylist(string playlistName = "Playlist", CoreSettings settings = null)
        {
            var library = CreateLibrary(settings);
            library.AddAndSwitchToPlaylist(playlistName, library.LocalAccessControl.RegisterLocalAccessToken());

            return library;
        }

        public static string GenerateSaveFile()
        {
            using (var stream = new MemoryStream())
            {
                LibraryWriter.Write(new[] { LocalSong1, LocalSong2 }, new[] { Playlist1, Playlist2 }, SongSourcePath, stream);

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static Song SetupSongMock(string name = "Song", bool callBase = false, TimeSpan duration = new TimeSpan())
        {
            var mock = Substitute.For<Song>(name, duration);
            mock.PrepareAsync(Arg.Any<YoutubeStreamingQuality>()).Returns(Task.Delay(0));

            return mock;
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

        public async static Task<T> ThrowsAsync<T>(Func<Task> testCode) where T : Exception
        {
            try
            {
                await testCode();
                Assert.Throws<T>(() => { }); // Use xUnit's default behavior.
            }
            catch (T exception)
            {
                return exception;
            }

            return null;
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

        private static ILocalSongFinder SetupDefaultLocalSongFinder()
        {
            var localSongFinder = Substitute.For<ILocalSongFinder>();
            localSongFinder.GetSongsAsync().Returns(Observable.Empty<Tuple<LocalSong, byte[]>>());

            return localSongFinder;
        }
    }
}