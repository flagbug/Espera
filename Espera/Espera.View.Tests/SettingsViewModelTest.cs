using Caliburn.Micro;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.Services;
using Espera.View.ViewModels;
using NSubstitute;
using ReactiveUI;
using System;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Linq;
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
            var vm = new SettingsViewModel(library, new ViewSettings(), new CoreSettings(), Substitute.For<IWindowManager>(), token,
                new MobileApiInfo(Observable.Return(0), Observable.Return(false)));

            var coll = vm.UpdateLibraryCommand.CanExecuteObservable.CreateCollection();

            library.ChangeSongSourcePath("C://Test", token);

            var expected = new[] { false, true };

            Assert.Equal(expected, coll);
        }
    }
}