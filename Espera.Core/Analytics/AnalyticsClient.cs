using Akavache;
using Espera.Core.Settings;
using Splat;
using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Globalization;
using System.Reactive.Disposables;
using Xamarin;

namespace Espera.Core.Analytics
{
    /// <summary>
    /// Provides methods to measure data or report crashes/bugs. Every method either doesn't throw
    /// an exception if it fails or returns a <c>bool</c> , so they can be "fire-and-forget"
    /// methods. This also affects the authentication, meaning that if the initial authentication to
    /// the analytics provider fails, calls to the analytics methods will return immediately.
    /// </summary>
    public class AnalyticsClient : IEnableLogger, IDisposable
    {
        private static readonly Lazy<AnalyticsClient> instance;
        private readonly IAnalyticsEndpoint endpoint;
        private CoreSettings coreSettings;

        static AnalyticsClient()
        {
            instance = new Lazy<AnalyticsClient>(() => new AnalyticsClient());
        }

        public AnalyticsClient(IAnalyticsEndpoint endpoint = null)
        {
            this.endpoint = endpoint ?? new XamarinAnalyticsEndpoint();
        }

        public static AnalyticsClient Instance
        {
            get
            {
                bool isDebugging = false;
#if DEBUG
                isDebugging = true;
#endif
                if (ModeDetector.InUnitTestRunner() || isDebugging)
                {
                    var client = new AnalyticsClient(new DummyAnalyticsEndpoint());
                    client.Initialize(new CoreSettings(new InMemoryBlobCache()) { EnableAutomaticReports = false });
                    return client;
                }

                return instance.Value;
            }
        }

        public bool EnableAutomaticReports
        {
            get { return this.coreSettings.EnableAutomaticReports; }
        }

        public void Dispose()
        {
            this.endpoint.Dispose();
        }

        public void Initialize(CoreSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.coreSettings = settings;

            this.endpoint.Initialize();

            this.Log().Info("Initialized the analytics and crash report provider");
            this.Log().Info("Automatic analytics are {0}", this.EnableAutomaticReports ? "Enabled" : "Disabled");
        }

        /// <summary>
        /// Submits a bugreport with the specified message an an optional email address. Also
        /// uploads the log file located in the application data folder.
        /// </summary>
        /// <param name="message">The bugreport message.</param>
        /// <param name="email">The optional email address. Pass null if no email should be sent.</param>
        /// <returns>A task that returns whether the report was successfully sent or not.</returns>
        public void RecordBugReport(string message, string email = null)
        {
            if (!String.IsNullOrWhiteSpace(email))
            {
                this.endpoint.UpdateEmail(email);
            }

            try
            {
                this.endpoint.ReportBug(message);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send bug report", ex);
            }
        }

        /// <summary>
        /// Submits a crash report with the specified exception.
        /// </summary>
        /// <param name="exception">The exception that caused the application to crash.</param>
        /// <returns>A task that returns whether the report was successfully sent or not.</returns>
        public void RecordCrash(Exception exception)
        {
            try
            {
                this.endpoint.ReportFatalException(exception);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send crash report", ex);
            }
        }

        public void RecordLibrarySize(int songCount)
        {
            if (!this.EnableAutomaticReports)
                return;

            try
            {
                var traits = new Dictionary<string, string>
                {
                    { "Size", songCount.ToString(CultureInfo.InvariantCulture) }
                };

                this.endpoint.Track("Library Lookup", traits);
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Could not log library size", ex);
            }
        }

        public void RecordMobileUsage()
        {
            if (!this.EnableAutomaticReports)
                return;

            try
            {
                this.endpoint.Track("Connected Mobile API");
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Could not log mobile usage", ex);
            }
        }

        public void RecordNonFatalError(Exception exception)
        {
            if (!this.EnableAutomaticReports)
                return;

            try
            {
                this.endpoint.ReportNonFatalException(exception);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send error report", ex);
            }
        }

        public IDisposable RecordTime(string key, IDictionary<string, string> traits = null)
        {
            if (!this.EnableAutomaticReports)
                return Disposable.Empty;

            return this.endpoint.TrackTime(key, traits);
        }
    }
}