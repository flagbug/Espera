using System;
using System.Linq;
using Espera.Core.Management;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using Xunit;

namespace Espera.View.Tests
{
    public class PlaylistViewModelTest
    {
        public class TheNameEditingBehavior
        {
            [Fact]
            public void ExistingNameDoesNotValidate()
            {
                using (Library library = new LibraryBuilder().Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.AddPlaylist("Existing", accessToken);
                    library.AddPlaylist("New", accessToken);

                    var fixture = new PlaylistViewModel(library.Playlists.Single(x => x.Name == "New"), library);

                    fixture.EditName = true;
                    fixture.Name = "Existing";

                    Assert.NotNull(fixture["Name"]);
                }
            }
        }
    }
}