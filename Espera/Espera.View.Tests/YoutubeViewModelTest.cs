using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using Microsoft.Reactive.Testing;
using NSubstitute;
using ReactiveUI;
using ReactiveUI.Testing;
using Xunit;

namespace Espera.View.Tests
{
    public class YoutubeViewModelTest
    {
        [Fact]
        public void AvailableNetworkStartsSongSearch()
        {
            var isAvailable = new BehaviorSubject<bool>(false);
            var networkStatus = Substitute.For<INetworkStatus>();
            networkStatus.GetIsAvailableAsync().Returns(isAvailable);

            var songFinder = Substitute.For<IYoutubeSongFinder>();
            songFinder.GetSongsAsync(Arg.Any<string>())
                .Returns(Task.FromResult((IReadOnlyList<YoutubeSong>)new List<YoutubeSong>()));

            using (var library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, networkStatus, songFinder);

                isAvailable.OnNext(true);

                songFinder.ReceivedWithAnyArgs(1).GetSongsAsync(null);
            }
        }

        [Fact]
        public void OldRequestIsThrownAwayIfNewOneArrives()
        {
            var networkStatus = Substitute.For<INetworkStatus>();
            networkStatus.GetIsAvailableAsync().Returns(Observable.Return(true));

            using (var library = Helpers.CreateLibrary())
            {
                new TestScheduler().With(scheduler =>
                {
                    var songs = new[] { new YoutubeSong("http://blabla.com", TimeSpan.Zero) };

                    // Define that the old request takes longer than the new request
                    var firstReturn = Observable.Timer(TimeSpan.FromSeconds(2000), scheduler).Select(x => (IReadOnlyList<YoutubeSong>)new List<YoutubeSong>()).ToTask();
                    var secondReturn = Task.FromResult((IReadOnlyList<YoutubeSong>)new List<YoutubeSong>(songs));

                    var songFinder = Substitute.For<IYoutubeSongFinder>();
                    songFinder.GetSongsAsync(Arg.Any<string>()).Returns(firstReturn, secondReturn);

                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, networkStatus, songFinder);

                    vm.SearchText = "Request1";

                    scheduler.AdvanceByMs(501);

                    vm.SearchText = "Request2";

                    scheduler.AdvanceByMs(501);

                    scheduler.AdvanceByMs(2000);

                    Assert.Equal(songs.First().OriginalPath, vm.SelectableSongs.First().Path);
                });
            }
        }

        [Fact]
        public void SearchTextChangeSetsIsSearchingTest()
        {
            var networkStatus = Substitute.For<INetworkStatus>();
            networkStatus.GetIsAvailableAsync().Returns(Observable.Return(true));

            var songFinder = Substitute.For<IYoutubeSongFinder>();
            songFinder.GetSongsAsync(Arg.Any<string>()).Returns(Task.FromResult((IReadOnlyList<YoutubeSong>)new List<YoutubeSong>()));

            using (var library = Helpers.CreateLibrary())
            {
                new TestScheduler().With(scheduler =>
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, networkStatus, songFinder);

                    var isSearching = vm.WhenAnyValue(x => x.IsSearching).CreateCollection();

                    vm.SearchText = "Trololo";

                    scheduler.AdvanceByMs(501);

                    Assert.Equal(new[] { false, true, false }, isSearching);
                });
            }
        }

        [Fact]
        public void SmokeTest()
        {
            var song1 = new YoutubeSong("www.youtube.com?watch=abcde", TimeSpan.Zero) { Title = "A" };
            var song2 = new YoutubeSong("www.youtube.com?watch=abcdef", TimeSpan.Zero) { Title = "B" };

            var songs = (IReadOnlyList<YoutubeSong>)new[] { song1, song2 }.ToList();

            var networkStatus = Substitute.For<INetworkStatus>();
            networkStatus.GetIsAvailableAsync().Returns(Observable.Return(true));

            var songFinder = Substitute.For<IYoutubeSongFinder>();
            songFinder.GetSongsAsync(Arg.Any<string>()).Returns(Task.FromResult(songs));

            using (var library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, networkStatus, songFinder);

                Assert.Equal(songs, vm.SelectableSongs.Select(x => x.Model).ToList());
                Assert.Equal(songs.First(), vm.SelectableSongs.First().Model);
                Assert.False(vm.IsSearching);
            }
        }

        [Fact]
        public void SongFinderExceptionSetsIsNetworkUnavailableToTrue()
        {
            var isAvailable = new BehaviorSubject<bool>(false);
            var networkStatus = Substitute.For<INetworkStatus>();
            networkStatus.GetIsAvailableAsync().Returns(isAvailable);

            var songFinder = Substitute.For<IYoutubeSongFinder>();
            songFinder.GetSongsAsync(Arg.Any<string>()).Returns(x => { throw new Exception(); });

            using (var library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, networkStatus, songFinder);

                var isNetworkUnavailable = vm.WhenAnyValue(x => x.IsNetworkUnavailable).CreateCollection();

                isAvailable.OnNext(true);

                Assert.Equal(new[] { true, false, true }, isNetworkUnavailable);
            }
        }

        [Fact]
        public void UnavailableNetworkSmokeTest()
        {
            var isAvailable = new BehaviorSubject<bool>(false);
            var networkStatus = Substitute.For<INetworkStatus>();
            networkStatus.GetIsAvailableAsync().Returns(isAvailable);

            var songFinder = Substitute.For<IYoutubeSongFinder>();
            songFinder.GetSongsAsync(Arg.Any<string>()).Returns(Task.FromResult((IReadOnlyList<YoutubeSong>)new List<YoutubeSong>()));

            using (var library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, networkStatus, songFinder);

                var isNetworkUnavailable = vm.WhenAnyValue(x => x.IsNetworkUnavailable).CreateCollection();

                isAvailable.OnNext(true);

                Assert.Equal(new[] { true, false }, isNetworkUnavailable);
            }
        }

        public class TheRefreshNetworkAvailabilityCommand
        {
            [Fact]
            public void SmokeTest()
            {
                var networkStatus = Substitute.For<INetworkStatus>();
                networkStatus.GetIsAvailableAsync().Returns(Observable.Return(false), Observable.Return(true));

                var songFinder = Substitute.For<IYoutubeSongFinder>();
                songFinder.GetSongsAsync(Arg.Any<string>()).Returns(Task.FromResult((IReadOnlyList<YoutubeSong>)new List<YoutubeSong>()));

                using (var library = Helpers.CreateLibrary())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, networkStatus, songFinder);

                    Assert.True(vm.IsNetworkUnavailable);

                    vm.RefreshNetworkAvailabilityCommand.Execute(null);

                    Assert.False(vm.IsNetworkUnavailable);
                }
            }
        }
    }
}