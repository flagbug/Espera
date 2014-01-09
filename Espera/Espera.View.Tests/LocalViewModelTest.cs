using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;
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