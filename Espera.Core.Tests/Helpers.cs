using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using Espera.Core.Management;
using Espera.Core.Settings;
using Microsoft.Reactive.Testing;
using NSubstitute;
using ReactiveUI;
using ReactiveUI.Testing;
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
            Task updateCompleted = null;

            new TestScheduler().With(sched =>
            {
                library.Initialize();

                updateCompleted = Observable.If(() => String.IsNullOrEmpty(library.SongSourcePath),
                    Observable.Return(Unit.Default),
                    library.WhenAnyValue(x => x.IsUpdating).Where(x => !x).Skip(1).FirstAsync().Timeout(TimeSpan.FromSeconds(5)).Select(_ => Unit.Default)).ToTask();

                sched.AdvanceByMs(Library.InitialUpdateDelay.TotalMilliseconds + 1);
            });

            await updateCompleted;
        }

        public static Library CreateLibrary(CoreSettings settings = null, ILibraryReader reader = null, ILibraryWriter writer = null,
            IFileSystem fileSystem = null, ILocalSongFinder localSongFinder = null)
        {
            return new LibraryBuilder().WithReader(reader)
                .WithWriter(writer)
                .WithSettings(settings)
                .WithFileSystem(fileSystem)
                .WithSongFinder(localSongFinder)
                .Build();
        }

        public static string GenerateSaveFile()
        {
            using (var stream = new MemoryStream())
            {
                LibrarySerializer.Serialize(new[] { LocalSong1, LocalSong2 }, new[] { Playlist1, Playlist2 }, SongSourcePath, stream);

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static Song SetupSongMock(string name = "Song", TimeSpan duration = new TimeSpan())
        {
            var mock = Substitute.For<Song>(Path.Combine("C://", Guid.NewGuid().ToString(), ".mp3"), duration);
            mock.Title = name;
            mock.PrepareAsync(Arg.Any<YoutubeStreamingQuality>()).Returns(Task.Delay(0));
            mock.PlaybackPath.Returns(mock.OriginalPath);

            return mock;
        }

        public static Song[] SetupSongMocks(int count)
        {
            var songs = new Song[count];

            for (int i = 0; i < count; i++)
            {
                songs[i] = SetupSongMock("Song" + i);
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
    }
}