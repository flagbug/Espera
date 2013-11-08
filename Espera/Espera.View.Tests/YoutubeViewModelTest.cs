using Akavache;
using Espera.Core;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using Moq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Xunit;

namespace Espera.View.Tests
{
    public class YoutubeViewModelTest
    {
        [Fact]
        public void AvailableNetworkStartsSongSearch()
        {
            var isAvailable = new BehaviorSubject<bool>(false);
            var networkStatus = new Mock<INetworkStatus>();
            networkStatus.SetupGet(x => x.IsAvailable).Returns(isAvailable);

            var songFinder = new Mock<IYoutubeSongFinder>();
            songFinder.Setup(x => x.GetSongsAsync(It.IsAny<string>()))
                .Returns(Task.FromResult((IReadOnlyList<YoutubeSong>)new List<YoutubeSong>()))
                .Verifiable();

            using (var library = Helpers.CreateLibrary())
            {
                var vm = new YoutubeViewModel(library, new ViewSettings(new TestBlobCache()), new CoreSettings(new TestBlobCache()), networkStatus.Object, songFinder.Object);

                isAvailable.OnNext(true);

                songFinder.Verify(x => x.GetSongsAsync(It.IsAny<string>()), Times.Once);
            }
        }

        [Fact]
        public void SmokeTest()
        {
            var song1 = new YoutubeSong("www.youtube.com?watch=abcde", TimeSpan.Zero) { Title = "A" };
            var song2 = new YoutubeSong("www.youtube.com?watch=abcdef", TimeSpan.Zero) { Title = "B" };

            var songs = (IReadOnlyList<YoutubeSong>)new[] { song1, song2 }.ToList();

            var networkStatus = new Mock<INetworkStatus>();
            networkStatus.SetupGet(x => x.IsAvailable).Returns(Observable.Return(true));

            var songFinder = new Mock<IYoutubeSongFinder>();
            songFinder.Setup(x => x.GetSongsAsync(It.IsAny<string>())).Returns(Task.FromResult(songs));

            using (var library = Helpers.CreateLibrary())
            {
                var vm = new YoutubeViewModel(library, new ViewSettings(new TestBlobCache()), new CoreSettings(new TestBlobCache()), networkStatus.Object, songFinder.Object);

                Assert.Equal(songs, vm.SelectableSongs.Select(x => x.Model).ToList());
                Assert.Equal(songs.First(), vm.SelectableSongs.First().Model);
                Assert.False(vm.IsSearching);
            }
        }

        [Fact]
        public void SongFinderExceptionSetsIsNetworkUnavailableToTrue()
        {
            var isAvailable = new BehaviorSubject<bool>(false);
            var networkStatus = new Mock<INetworkStatus>();
            networkStatus.SetupGet(x => x.IsAvailable).Returns(isAvailable);

            var songFinder = new Mock<IYoutubeSongFinder>();
            songFinder.Setup(x => x.GetSongsAsync(It.IsAny<string>())).Throws<Exception>();

            using (var library = Helpers.CreateLibrary())
            {
                var vm = new YoutubeViewModel(library, new ViewSettings(new TestBlobCache()), new CoreSettings(new TestBlobCache()), networkStatus.Object, songFinder.Object);

                var isNetworkUnavailable = vm.WhenAnyValue(x => x.IsNetworkUnavailable).CreateCollection();

                isAvailable.OnNext(true);

                Assert.Equal(new[] { true, false, true }, isNetworkUnavailable);
            }
        }

        [Fact]
        public async Task StartSearchSetsIsSearchingTest()
        {
            var networkStatus = new Mock<INetworkStatus>();
            networkStatus.SetupGet(x => x.IsAvailable).Returns(Observable.Return(false));

            var songFinder = new Mock<IYoutubeSongFinder>();
            songFinder.Setup(x => x.GetSongsAsync(It.IsAny<string>())).Returns(Task.FromResult((IReadOnlyList<YoutubeSong>)new List<YoutubeSong>()));

            using (var library = Helpers.CreateLibrary())
            {
                var vm = new YoutubeViewModel(library, new ViewSettings(new TestBlobCache()), new CoreSettings(new TestBlobCache()), networkStatus.Object, songFinder.Object);

                var isSearching = vm.WhenAnyValue(x => x.IsSearching).CreateCollection();

                await vm.StartSearchAsync();

                Assert.Equal(new[] { false, true, false }, isSearching);
            }
        }

        [Fact]
        public void UnavailableNetworkSmokeTest()
        {
            var isAvailable = new BehaviorSubject<bool>(false);
            var networkStatus = new Mock<INetworkStatus>();
            networkStatus.SetupGet(x => x.IsAvailable).Returns(isAvailable);

            var songFinder = new Mock<IYoutubeSongFinder>();
            songFinder.Setup(x => x.GetSongsAsync(It.IsAny<string>())).Returns(Task.FromResult((IReadOnlyList<YoutubeSong>)new List<YoutubeSong>()));

            using (var library = Helpers.CreateLibrary())
            {
                var vm = new YoutubeViewModel(library, new ViewSettings(new TestBlobCache()), new CoreSettings(new TestBlobCache()), networkStatus.Object, songFinder.Object);

                var isNetworkUnavailable = vm.WhenAnyValue(x => x.IsNetworkUnavailable).CreateCollection();

                isAvailable.OnNext(true);

                Assert.Equal(new[] { true, false }, isNetworkUnavailable);
            }
        }
    }
}