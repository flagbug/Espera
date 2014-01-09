using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using ReactiveUI;
using System;
using Xunit;

namespace Espera.View.Tests
{
    public class LocalViewModelTest
    {
        [Fact]
        public void HasAllArtistsForEmptyLibrary()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                var vm = new LocalViewModel(library, new ViewSettings(), new CoreSettings(), accessToken);

                Assert.Equal(1, vm.Artists.Count);
                Assert.True(vm.Artists[0].IsAllArtists);
                Assert.Equal(vm.Artists[0], vm.SelectedArtist);
            }
        }

        [Fact]
        public void PlayNowCommandCanExecuteSmokeTest()
        {
            var settings = new CoreSettings();

            using (Library library = Helpers.CreateLibrary(settings))
            {
                Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                var vm = new LocalViewModel(library, new ViewSettings(), settings, accessToken);

                var canExecuteColl = vm.PlayNowCommand.CanExecuteObservable.CreateCollection();

                library.LocalAccessControl.SetLocalPassword(accessToken, "Password");
                library.LocalAccessControl.DowngradeLocalAccess(accessToken);

                Assert.Equal(new[] { true, false }, canExecuteColl);
            }
        }

        [Fact]
        public void ShowSongHelperMessageIsTrueForEmptyLibrary()
        {
            using (Library library = Helpers.CreateLibrary())
            {
                Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                var vm = new LocalViewModel(library, new ViewSettings(), new CoreSettings(), accessToken);

                Assert.True(vm.ShowAddSongsHelperMessage);
            }
        }
    }
}