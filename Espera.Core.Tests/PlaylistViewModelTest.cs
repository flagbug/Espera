using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;

namespace Espera.View.Tests
{
    public class PlaylistViewModelTest
    {
        public class TheNameEditingBehavior
        {
            [Fact]
            public void ExistingNameDoesNotValidate()
            {
                using (var library = new LibraryBuilder().Build())
                {
                    var accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.AddPlaylist("Existing", accessToken);
                    library.AddPlaylist("New", accessToken);

                    var fixture = new PlaylistViewModel(library.Playlists.Single(x => x.Name == "New"), library,
                        accessToken, new CoreSettings());

                    fixture.EditName = true;
                    fixture.Name = "Existing";

                    Assert.NotNull(fixture["Name"]);
                }
            }

            [Fact]
            public void UniqueNameDoesValidate()
            {
                using (var library = new LibraryBuilder().Build())
                {
                    var accessToken = library.LocalAccessControl.RegisterLocalAccessToken();
                    library.AddPlaylist("Existing", accessToken);
                    library.AddPlaylist("New", accessToken);

                    var fixture = new PlaylistViewModel(library.Playlists.Single(x => x.Name == "New"), library,
                        accessToken, new CoreSettings());

                    fixture.EditName = true;
                    fixture.Name = "Unique";

                    Assert.Null(fixture["Name"]);
                }
            }
        }
    }
}