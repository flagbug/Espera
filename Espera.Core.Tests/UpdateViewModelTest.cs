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
                using (var vm = new UpdateViewModel(settings) { DisableChangelog = true })
                {
                    vm.ChangelogShown();
                }

                Assert.False(settings.EnableChangelog);
            }
        }

        public class TheDismissUpdateNotificationMethod
        {
            [Fact]
            public void SetsIsUpdatedToFalse()
            {
                var settings = new ViewSettings { IsUpdated = true };
                using (var vm = new UpdateViewModel(settings))
                {
                    vm.DismissUpdateNotification();
                }

                Assert.False(settings.IsUpdated);
            }
        }

        public class TheShowChangelogProperty
        {
            [Fact]
            public void IsFalseIfNotUpdated()
            {
                var settings = new ViewSettings { IsUpdated = false };
                using (var vm = new UpdateViewModel(settings))
                {
                    Assert.False(vm.ShowChangelog);
                }
            }

            [Fact]
            public void IsFalseIfUpdatedButOptedOut()
            {
                var settings = new ViewSettings { IsUpdated = true, EnableChangelog = false };
                using (var vm = new UpdateViewModel(settings))
                {
                    Assert.False(vm.ShowChangelog);
                }
            }

            [Fact]
            public void IsTrueIfUpdatedAndOptedIn()
            {
                var settings = new ViewSettings { IsUpdated = true, EnableChangelog = true };
                using (var vm = new UpdateViewModel(settings))
                {
                    Assert.True(vm.ShowChangelog);
                }
            }
        }
    }
}