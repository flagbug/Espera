using System;
using System.Linq;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Settings;
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

                using (var library = new LibraryBuilder().WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    var playlist = library.Playlists.First();
                    library.SwitchToPlaylist(playlist, accessToken);

                    var fixture = new DirectYoutubeViewModel(library, new CoreSettings(), accessToken, songFinder);

                    await Helpers.ThrowsAsync<ArgumentNullException>(() => fixture.AddDirectYoutubeUrlToPlaylist(null, null));
                }
            }

            [Fact]
            public async Task NullYoutubeSongFinderResultDoesNothing()
            {
                var songFinder = Substitute.For<IYoutubeSongFinder>();
                songFinder.ResolveYoutubeSongFromUrl(Arg.Any<Uri>()).Returns(Task.FromResult<YoutubeSong>(null));

                using (var library = new LibraryBuilder().WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    var playlist = library.Playlists.First();
                    library.SwitchToPlaylist(playlist, accessToken);

                    var fixture = new DirectYoutubeViewModel(library, new CoreSettings(), accessToken, songFinder);

                    await fixture.AddDirectYoutubeUrlToPlaylist(new Uri("http://youtube.com?v=yadda"), null);

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

                using (var library = new LibraryBuilder().WithPlaylist().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    var playlist = library.Playlists.First();
                    library.SwitchToPlaylist(playlist, accessToken);

                    var fixture = new DirectYoutubeViewModel(library, new CoreSettings(), accessToken, songFinder);

                    await fixture.AddDirectYoutubeUrlToPlaylist(new Uri(youtubePath), null);

                    Assert.Equal(1, playlist.Count());
                }
            }
        }
    }
}