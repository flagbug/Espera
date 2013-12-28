using Caliburn.Micro;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using Moq;
using ReactiveUI;
using System;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace Espera.View.Tests
{
    public class SettingsViewModelTest
    {
        [Fact]
        public void UpdateNowCommandCanExecuteSmokeTest()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory.CreateDirectory("C://Test");

            Library library = Helpers.CreateLibrary(fileSystem);
            Guid token = library.LocalAccessControl.RegisterLocalAccessToken();
            var vm = new SettingsViewModel(library, new ViewSettings(), new CoreSettings(), new Mock<IWindowManager>().Object, token);

            var coll = vm.UpdateLibraryCommand.CanExecuteObservable.CreateCollection();

            library.ChangeSongSourcePath("C://Test", token);

            var expected = new[] { false, true };

            Assert.Equal(expected, coll);
        }
    }
}