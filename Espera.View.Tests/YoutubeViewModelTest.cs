using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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
        public void OldRequestIsThrownAwayIfNewOneArrives()
        {
            using (var library = Helpers.CreateLibrary())
            {
                new TestScheduler().With(scheduler =>
                {
                    var songs = new[] { new YoutubeSong("http://blabla.com", TimeSpan.Zero) };

                    // Define that the old request takes longer than the new request
                    var firstReturn = Observable.Timer(TimeSpan.FromSeconds(2000), scheduler).Select(x => new List<YoutubeSong>());
                    var secondReturn = Observable.Return(new List<YoutubeSong>(songs));

                    var songFinder = Substitute.For<IYoutubeSongFinder>();
                    songFinder.GetSongsAsync(Arg.Any<string>()).Returns(firstReturn, secondReturn);

                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, songFinder);

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
            var songFinder = Substitute.For<IYoutubeSongFinder>();
            songFinder.GetSongsAsync(Arg.Any<string>()).Returns(Observable.Return(new List<YoutubeSong>()));

            using (var library = Helpers.CreateLibrary())
            {
                new TestScheduler().With(scheduler =>
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, songFinder);

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

            var songFinder = Substitute.For<IYoutubeSongFinder>();
            songFinder.GetSongsAsync(Arg.Any<string>()).Returns(Observable.Return(songs));

            using (var library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, songFinder);

                Assert.Equal(songs, vm.SelectableSongs.Select(x => x.Model).ToList());
                Assert.Equal(songs.First(), vm.SelectableSongs.First().Model);
                Assert.False(vm.IsSearching);
            }
        }

        [Fact]
        public void SongFinderExceptionSetsSearchFailedToTrue()
        {
            var songFinder = Substitute.For<IYoutubeSongFinder>();
            songFinder.GetSongsAsync(Arg.Any<string>()).Returns(x => { throw new NetworkSongFinderException("Blabla", null); });

            using (var library = Helpers.CreateLibrary())
            {
                Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, songFinder);

                Assert.True(vm.SearchFailed);
            }
        }

        public class TheSearchCommand
        {
            [Fact]
            public async Task SmokeTest()
            {
                var songFinder = Substitute.For<IYoutubeSongFinder>();
                songFinder.GetSongsAsync(Arg.Any<string>()).Returns(Observable.Return(new List<YoutubeSong>()));

                using (var library = Helpers.CreateLibrary())
                {
                    Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
                    var vm = new YoutubeViewModel(library, new ViewSettings(), new CoreSettings(), token, songFinder);

                    await vm.Search.ExecuteAsync();

                    songFinder.Received(2).GetSongsAsync(string.Empty);
                }
            }
        }
    }
}