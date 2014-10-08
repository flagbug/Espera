using System;
using System.Reactive.Subjects;
using System.Reflection;
using Espera.Core.Settings;
using Splat;
using Xamarin;

namespace Espera.Core.Analytics
{
    /// <summary>
    /// Provides methods to measure data or report crashes/bugs. Every method either doesn't throw
    /// an exception if it fails or returns a <c>bool</c> , so they can be "fire-and-forget"
    /// methods. This also affects the authentication, meaning that if the initial authentication to
    /// the analytics provider fails, calls to the analytics methods will return immediately.
    /// </summary>
    public class AnalyticsClient : IEnableLogger
    {
        private static readonly Lazy<AnalyticsClient> instance;
        private readonly BehaviorSubject<bool> isAuthenticating;
        private CoreSettings coreSettings;
        private bool isAuthenticated;

        static AnalyticsClient()
        {
            instance = new Lazy<AnalyticsClient>(() => new AnalyticsClient());
        }

        public static AnalyticsClient Instance
        {
            get { return instance.Value; }
        }

        public bool EnableAutomaticReports
        {
            get { return this.coreSettings.EnableAutomaticReports; }
        }

        public void Initialize(CoreSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.coreSettings = settings;

            // If we don't have permission to send things, do nothing
            if (!this.coreSettings.EnableAutomaticReports)
                return;

            Insights.Initialize(null, Assembly.GetExecutingAssembly().GetName().Version.ToString(), "Espera");
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
                //Insights.Identify(Insights.T);
            }

            try
            {
                // The new Xamarin insights API only accepts exceptions, so we wrap the user message
                // into an exception as a workaround
                var exception = new Exception(message);

                Insights.Report(exception);
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
                Insights.Report(exception, ReportSeverity.Error);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send crash report", ex);
            }
        }

        public void RecordError(Exception exception)
        {
            try
            {
                Insights.Report(exception);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send error report", ex);
            }
        }

        public void RecordLibrarySize(int songCount)
        {
            try
            {
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Could not log library size", ex);
            }
        }

        public void RecordMobileUsage()
        {
            try
            {
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Could not log mobile usage", ex);
            }
        }
    }
}