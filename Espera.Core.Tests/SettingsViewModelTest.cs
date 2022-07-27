using System.Collections.Generic;
using Espera.Core.Mobile;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;

namespace Espera.View.Tests
{
    public class SettingsViewModelTest
    {
        [Fact]
        public void UpdateNowCommandCanExecuteSmokeTest()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            var library = new LibraryBuilder().WithFileSystem(fileSystem).Build();
            var token = library.LocalAccessControl.RegisterLocalAccessToken();
            var vm = new SettingsViewModel(library, new ViewSettings(), new CoreSettings(),
                Substitute.For<IWindowManager>(), token,
                new MobileApiInfo(Observable.Return(new List<MobileClient>()), Observable.Return(false)));

            var coll = vm.UpdateLibraryCommand.CanExecuteObservable.CreateCollection();

            library.ChangeSongSourcePath("C://Test", token);

            var expected = new[] { false, true };

            Assert.Equal(expected, coll);
        }
    }
}