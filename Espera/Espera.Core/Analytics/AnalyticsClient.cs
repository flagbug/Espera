using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Espera.Core.Settings;
using Splat;

namespace Espera.Core.Analytics
{
    /// <summary>
    /// Provides methods to measure data or report crashes/bugs. Every method either doesn't throw
    /// an exception if it fails or returns a <c>bool</c>, so they can be "fire-and-forget" methods.
    /// This also affects the authentication, meaning that if the initial authentication to the
    /// analytics provider fails, calls to the analytics methods will return immediately.
    /// </summary>
    public class AnalyticsClient : IEnableLogger
    {
        private static readonly Lazy<AnalyticsClient> instance;
        private readonly IAnalyticsEndpoint client;
        private readonly BehaviorSubject<bool> isAuthenticating;
        private CoreSettings coreSettings;
        private bool isAuthenticated;

        static AnalyticsClient()
        {
            instance = new Lazy<AnalyticsClient>(() => new AnalyticsClient());
        }

        public AnalyticsClient(IAnalyticsEndpoint analyticsEndpoint = null)
        {
            this.client = analyticsEndpoint ?? new BuddyAnalyticsEndpoint();
            this.isAuthenticating = new BehaviorSubject<bool>(false);
        }

        public static AnalyticsClient Instance
        {
            get { return instance.Value; }
        }

        public bool EnableAutomaticReports
        {
            get { return this.coreSettings.EnableAutomaticReports; }
        }

        /// <summary>
        /// Used for unit testing
        /// </summary>
        internal bool IsAuthenticated
        {
            get { return this.isAuthenticated; }
        }

        public async Task InitializeAsync(CoreSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.coreSettings = settings;

            // If we don't have permission to send things, do nothing
            if (!this.coreSettings.EnableAutomaticReports)
                return;

            await this.AuthenticateAsync();
        }

        /// <summary>
        /// Submits a bugreport with the specified message an an optional email address. Also
        /// uploads the log file located in the application data folder.
        /// </summary>
        /// <param name="message">The bugreport message.</param>
        /// <param name="email">The optional email address. Pass null if no email should be sent.</param>
        /// <returns>A task that returns whether the report was successfully sent or not.</returns>
        public async Task<bool> RecordBugReportAsync(string message, string email = null)
        {
            // Bugreports always have forced authentication as they are reported manually
            if (!await this.AwaitAuthenticationAsync(true))
                return false;

            if (!String.IsNullOrWhiteSpace(email))
            {
                await this.client.UpdateUserEmailAsync(email);
            }

            try
            {
                // The new Buddy API only accepts exceptions, so we wrap the user message into an
                // exception as a workaround
                var exception = new Exception(message);
                await this.client.RecordErrorAsync(exception);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send bug report", ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Submits a crash report with the specified exception. Also uploads the log file located
        /// in the application data folder.
        /// </summary>
        /// <param name="exception">The exception that caused the application to crash.</param>
        /// <returns>A task that returns whether the report was successfully sent or not.</returns>
        public async Task<bool> RecordCrashAsync(Exception exception)
        {
            if (!await this.AwaitAuthenticationAsync(true))
                return false;

            try
            {
                await this.client.RecordErrorAsync(exception);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send crash report", ex);
                return false;
            }

            return true;
        }

        public async Task RecordErrorAsync(Exception exception)
        {
            if (!await this.AwaitAuthenticationAsync())
                return;

            try
            {
                await this.client.RecordErrorAsync(exception);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send error report", ex);
            }
        }

        public async Task RecordLibrarySizeAsync(int songCount)
        {
            if (!await this.AwaitAuthenticationAsync())
                return;

            try
            {
                await this.client.RecordLibrarySizeAsync(songCount);
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Could not log library size", ex);
            }
        }

        public async Task RecordMobileUsage()
        {
            if (!await this.AwaitAuthenticationAsync())
                return;

            try
            {
                await this.client.RecordMobileUsageAsync();
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Could not log mobile usage", ex);
            }
        }

        private async Task AuthenticateAsync()
        {
            this.isAuthenticating.OnNext(true);

            try
            {
                if (this.coreSettings.AnalyticsToken == null || !this.coreSettings.BuddyAnalyticsUpgraded)
                {
                    string analyticsToken = await this.client.CreateUserAsync();
                    this.coreSettings.AnalyticsToken = analyticsToken;
                    this.coreSettings.BuddyAnalyticsUpgraded = true;

                    this.Log().Info("Created new analytics user");
                }

                else
                {
                    await this.client.AuthenticateUserAsync(this.coreSettings.AnalyticsToken);

                    this.Log().Info("Logged into the analytics provider");
                }

                this.isAuthenticated = true;
            }

            // Don't care which exception is thrown, if something bad happens the analytics are unusable
            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't login to the analytics server", ex);
            }

            finally
            {
                this.isAuthenticating.OnNext(false);
            }

            if (this.isAuthenticated)
            {
                try
                {
                    await this.client.RecordDeviceInformationAsync();
                    await this.client.RecordLanguageAsync();
                    await this.client.RecordDeploymentType();
                }

                catch (Exception ex)
                {
                    this.Log().ErrorException("Could not record device information", ex);
                }
            }
        }

        private async Task<bool> AwaitAuthenticationAsync(bool force = false)
        {
            if (!this.coreSettings.EnableAutomaticReports && !force)
                return false;

            // We aren't authenticated but the user allows us the send data? Authenticate!
            if (!this.isAuthenticated && (this.coreSettings.EnableAutomaticReports || force))
            {
                try
                {
                    await this.AuthenticateAsync();
                }

                catch (Exception)
                {
                    return false;
                }
            }

            await this.isAuthenticating.FirstAsync(x => !x);

            return this.isAuthenticated;
        }
    }
}