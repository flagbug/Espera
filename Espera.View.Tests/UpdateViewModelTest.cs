using Espera.View.ViewModels;
using Xunit;

namespace Espera.View.Tests
{
    public class UpdateViewModelTest
    {
        public class TheChangelogShownMethod
        {
            [Fact]
            public void DisabledChangelogIfRequested()
            {
                var settings = new ViewSettings { IsUpdated = true, EnableChangelog = true };
                var vm = new UpdateViewModel(settings) { DisableChangelog = true };

                vm.ChangelogShown();

                Assert.False(settings.EnableChangelog);
            }

            [Fact]
            public void SetsIsUpdatedToFalse()
            {
                var settings = new ViewSettings { IsUpdated = true };
                var vm = new UpdateViewModel(settings);

                vm.ChangelogShown();

                Assert.False(settings.IsUpdated);
            }
        }

        public class TheShowChangelogProperty
        {
            [Fact]
            public void IsFalseIfNotUpdated()
            {
                var settings = new ViewSettings { IsUpdated = false };
                var vm = new UpdateViewModel(settings);

                Assert.False(vm.ShowChangelog);
            }

            [Fact]
            public void IsFalseIfUpdatedButOptedOut()
            {
                var settings = new ViewSettings { IsUpdated = true, EnableChangelog = false };
                var vm = new UpdateViewModel(settings);

                Assert.False(vm.ShowChangelog);
            }

            [Fact]
            public void IsTrueIfUpdatedAndOptedIn()
            {
                var settings = new ViewSettings { IsUpdated = true, EnableChangelog = true };
                var vm = new UpdateViewModel(settings);

                Assert.True(vm.ShowChangelog);
            }
        }
    }
}