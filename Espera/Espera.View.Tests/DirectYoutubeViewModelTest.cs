using System;
using System.Linq;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using NSubstitute;
using Xunit;

namespace Espera.View.Tests
{
    public class DirectYoutubeViewModelTest
    {
        public class TheAddDirectYoutubeUrlToPlaylistMethod
        {
            [Fact]
            public async Task NullUriThrowsArgumentNullException()
            {
                const string youtubePath = "http://youtube.com?v=yadda";
                var song = new YoutubeSong(youtubePath, TimeSpan.FromMinutes(1));
                var songFinder = Substitute.For<IYoutubeSongFinder>();
                songFinder.ResolveYoutubeSongFromUrl(Arg.Any<Uri>()).Returns(Task.FromResult(song));

                using (var library = Helpers.CreateLibraryWithPlaylist())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    var playlist = library.Playlists.First();
                    library.SwitchToPlaylist(playlist, accessToken);

                    var fixture = new DirectYoutubeViewModel(library, accessToken, songFinder);

                    await Helpers.ThrowsAsync<ArgumentNullException>(() => fixture.AddDirectYoutubeUrlToPlaylist(null));
                }
            }

            [Fact]
            public async Task NullYoutubeSongFinderResultDoesNothing()
            {
                var songFinder = Substitute.For<IYoutubeSongFinder>();
                songFinder.ResolveYoutubeSongFromUrl(Arg.Any<Uri>()).Returns(Task.FromResult<YoutubeSong>(null));

                using (var library = Helpers.CreateLibraryWithPlaylist())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    var playlist = library.Playlists.First();
                    library.SwitchToPlaylist(playlist, accessToken);

                    var fixture = new DirectYoutubeViewModel(library, accessToken, songFinder);

                    await fixture.AddDirectYoutubeUrlToPlaylist(new Uri("http://youtube.com?v=yadda"));

                    Assert.Equal(0, playlist.Count());
                }
            }

            [Fact]
            public async Task SmokeTest()
            {
                const string youtubePath = "http://youtube.com?v=yadda";
                var song = new YoutubeSong(youtubePath, TimeSpan.FromMinutes(1));
                var songFinder = Substitute.For<IYoutubeSongFinder>();
                songFinder.ResolveYoutubeSongFromUrl(Arg.Any<Uri>()).Returns(Task.FromResult(song));

                using (var library = Helpers.CreateLibraryWithPlaylist())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    var playlist = library.Playlists.First();
                    library.SwitchToPlaylist(playlist, accessToken);

                    var fixture = new DirectYoutubeViewModel(library, accessToken, songFinder);

                    await fixture.AddDirectYoutubeUrlToPlaylist(new Uri(youtubePath));

                    Assert.Equal(1, playlist.Count());
                }
            }
        }
    }
}