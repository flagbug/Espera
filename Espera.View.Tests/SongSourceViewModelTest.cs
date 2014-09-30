using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using NSubstitute;
using ReactiveUI;
using Xunit;

namespace Espera.View.Tests
{
    public class SongSourceViewModelTest
    {
        [Fact]
        public async Task PartyModeTriggersTimeoutMessage()
        {
            using (var library = new LibraryBuilder().WithPlaylist().Build())
            {
                Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                library.LocalAccessControl.SetLocalPassword(accessToken, "password");
                library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                var fixture = Substitute.For<SongSourceViewModel<ISongViewModelBase>>(library, accessToken);

                var timeoutTriggers = fixture.TimeoutWarning.CreateCollection();

                var songVm = Substitute.For<ISongViewModelBase>();
                var song = Helpers.SetupSongMock();
                songVm.Model.Returns(song);

                fixture.SelectedSongs = new[] { songVm };

                await fixture.AddToPlaylistCommand.ExecuteAsync();
                await fixture.AddToPlaylistCommand.ExecuteAsync();

                Assert.Equal(1, timeoutTriggers.Count);
            }
        }
    }
}