using Espera.Core.Analytics;
using Espera.Core.Settings;
using System.Threading.Tasks;
using Xunit;

namespace Espera.Core.Tests
{
    public class AnalyticsClientTest
    {
        [Fact]
        public async Task InitializeAsyncDoesntAuthenticeWithoutPermission()
        {
            var coreSettings = new CoreSettings { EnableAutomaticReports = false };

            var client = new AnalyticsClient();
            await client.InitializeAsync(coreSettings);

            Assert.False(client.IsAuthenticated);
        }
    }
}