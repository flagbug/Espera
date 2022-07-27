using System;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Akavache;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using NSubstitute;

namespace Espera.Core.Tests
{
    public class LibraryBuilder
    {
        private IMediaPlayerCallback audioPlayerCallback;
        private IFileSystem fileSystem;
        private string playlistName;
        private ILibraryReader reader;
        private CoreSettings settings;
        private ILocalSongFinder songFinder;
        private ILibraryWriter writer;

        public Library Build()
        {
            var finishSubject = new Subject<Unit>();
            var mediaPlayerCallback = Substitute.For<IMediaPlayerCallback>();
            mediaPlayerCallback.PlayAsync().Returns(_ => Task.Run(() => finishSubject.OnNext(Unit.Default)));
            mediaPlayerCallback.Finished.Returns(finishSubject);

            var library = new Library(
                reader ?? Substitute.For<ILibraryReader>(),
                writer ?? Substitute.For<ILibraryWriter>(),
                settings ?? new CoreSettings(new InMemoryBlobCache()),
                fileSystem ?? new MockFileSystem(),
                x => songFinder ?? SetupDefaultLocalSongFinder());

            var accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

            if (playlistName != null) library.AddAndSwitchToPlaylist(playlistName, accessToken);

            library.RegisterAudioPlayerCallback(audioPlayerCallback ?? mediaPlayerCallback, accessToken);

            return library;
        }

        public LibraryBuilder WithAudioPlayer(IMediaPlayerCallback player)
        {
            audioPlayerCallback = player;
            return this;
        }

        public LibraryBuilder WithFileSystem(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            return this;
        }

        public LibraryBuilder WithPlaylist(string name = "Playlist")
        {
            playlistName = name;
            return this;
        }

        public LibraryBuilder WithReader(ILibraryReader reader)
        {
            this.reader = reader;
            return this;
        }

        public LibraryBuilder WithSettings(CoreSettings settings)
        {
            this.settings = settings;
            return this;
        }

        public LibraryBuilder WithSongFinder(ILocalSongFinder songFinder)
        {
            this.songFinder = songFinder;
            return this;
        }

        public LibraryBuilder WithWriter(ILibraryWriter writer)
        {
            this.writer = writer;
            return this;
        }

        private static ILocalSongFinder SetupDefaultLocalSongFinder()
        {
            var localSongFinder = Substitute.For<ILocalSongFinder>();
            localSongFinder.GetSongsAsync().Returns(Observable.Empty<Tuple<LocalSong, byte[]>>());

            return localSongFinder;
        }
    }
}