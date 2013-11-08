using Akavache;
using Caliburn.Micro;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Core.Tests;
using Espera.View.ViewModels;
using Espera.View.Views;
using Moq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var vm = new SettingsViewModel(library, new ViewSettings(new TestBlobCache()), new CoreSettings(new TestBlobCache()), new Mock<IWindowManager>().Object);

            var coll = vm.UpdateLibraryCommand.CanExecuteObservable.CreateCollection();

            library.ChangeSongSourcePath("C://Test");

            var expected = new[] { false, true };

            Assert.Equal(expected, coll);
        }
    }
}