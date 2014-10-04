using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using NSubstitute;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Espera.View.Tests
{
    public class LocalViewModelTest
    {
        public class TheArtistsProperty
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
        }

        public class ThePlayNowCommand
        {
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
        }

        public class TheShowAddSongsHelperMessage
        {
            [Fact]
            public void IsTrueForEmptyLibrary()
            {
                using (Library library = Helpers.CreateLibrary())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    var vm = new LocalViewModel(library, new ViewSettings(), new CoreSettings(), accessToken);

                    Assert.True(vm.ShowAddSongsHelperMessage);
                }
            }

            [Fact]
            public async Task IsTrueForUpdatedLibrary()
            {
                var song = new LocalSong("C://Song.mp3", TimeSpan.Zero);

                var songFinder = Substitute.For<ILocalSongFinder>();
                songFinder.GetSongsAsync().Returns(Observable.Return(Tuple.Create(song, (byte[])null)));

                var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { { "C://Song.mp3", new MockFileData("Bla") } });

                using (Library library = new LibraryBuilder().WithFileSystem(fileSystem).WithSongFinder(songFinder).Build())
                {
                    Guid accessToken = library.LocalAccessControl.RegisterLocalAccessToken();

                    // NB: System.IO.Abstractions only likes backslashes
                    library.ChangeSongSourcePath("C:\\", accessToken);

                    var vm = new LocalViewModel(library, new ViewSettings(), new CoreSettings(), accessToken);

                    await library.AwaitInitializationAndUpdate();

                    Assert.False(vm.ShowAddSongsHelperMessage);
                }
            }
        }
    }
}