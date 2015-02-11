using Espera.Core.Analytics;
using Espera.Core.Settings;
using NSubstitute;
using System;
using Xunit;

namespace Espera.Core.Tests
{
    public class AnalyticsClientTest
    {
        public class TheInitializeMethod
        {
            [Fact]
            public void InitializesEndpoint()
            {
                var coreSettings = new CoreSettings { EnableAutomaticReports = true };
                var endpoint = Substitute.For<IAnalyticsEndpoint>();
                var client = new AnalyticsClient(endpoint);

                client.Initialize(coreSettings);

                endpoint.Received().Initialize();
            }
        }

        public class TheRecordBugReportMethod
        {
            [Fact]
            public void IgnoresEmailIfNullOrEmpty()
            {
                var coreSettings = new CoreSettings { EnableAutomaticReports = true };
                var endpoint = Substitute.For<IAnalyticsEndpoint>();
                var client = new AnalyticsClient(endpoint);
                client.Initialize(coreSettings);

                client.RecordBugReport("blabla");

                endpoint.DidNotReceiveWithAnyArgs().UpdateEmail(Arg.Any<string>());

                client.RecordBugReport("blabla", String.Empty);

                endpoint.DidNotReceiveWithAnyArgs().UpdateEmail(Arg.Any<string>());

                client.RecordBugReport("blabla", "  ");

                endpoint.DidNotReceiveWithAnyArgs().UpdateEmail(null);
            }

            [Fact]
            public void UpdatesEmailIfSet()
            {
                var coreSettings = new CoreSettings { EnableAutomaticReports = true };
                var endpoint = Substitute.For<IAnalyticsEndpoint>();
                var client = new AnalyticsClient(endpoint);
                client.Initialize(coreSettings);

                client.RecordBugReport("blabla", "email@email.com");

                endpoint.Received().UpdateEmail("email@email.com");
            }
        }

        public class TheRecordNonFatalErrorAsyncMethod
        {
            [Fact]
            public void RespectsDisabledReports()
            {
                var coreSettings = new CoreSettings { EnableAutomaticReports = false };
                var endpoint = Substitute.For<IAnalyticsEndpoint>();
                var client = new AnalyticsClient(endpoint);
                client.Initialize(coreSettings);

                client.RecordNonFatalError(new Exception());

                endpoint.DidNotReceiveWithAnyArgs().ReportNonFatalException(null);
            }

            [Fact]
            public void SendsReport()
            {
                var coreSettings = new CoreSettings { EnableAutomaticReports = true };
                var endpoint = Substitute.For<IAnalyticsEndpoint>();
                var client = new AnalyticsClient(endpoint);
                client.Initialize(coreSettings);

                client.RecordNonFatalError(new Exception());

                endpoint.ReceivedWithAnyArgs().ReportNonFatalException(null);
            }
        }
    }
}