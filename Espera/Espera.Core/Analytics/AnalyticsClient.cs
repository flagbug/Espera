using Espera.Core.Settings;
using ReactiveUI;
using System;
using System.IO;
using System.IO.Compression;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

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
        /// <param name="email">
        /// The optional email address. Pass null if no email should be sent.
        /// </param>
        /// <returns>A task that returns whether the report was successfully sent or not.</returns>
        public async Task<bool> RecordBugReportAsync(string message, string email = null)
        {
            // Bugreports always have forced authentication as they are reported manually
            if (!await this.AwaitAuthenticationAsync(true))
                return false;

            if (email != null)
            {
                await this.client.UpdateUserEmailAsync(email);
            }

            string logId = await this.SendLogFileAsync() ?? String.Empty;

            try
            {
                await this.client.RecordErrorAsync(message, logId);
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

            string logId = await this.SendLogFileAsync() ?? String.Empty;

            try
            {
                await this.client.RecordErrorAsync(exception.Message, logId, exception.StackTrace);
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't send crash report", ex);
                return false;
            }

            return true;
        }

        public async Task RecordErrorAsync(Exception exception, bool uploadLogFile = true)
        {
            if (!await this.AwaitAuthenticationAsync())
                return;

            string logId = uploadLogFile ? await this.SendLogFileAsync() ?? String.Empty : String.Empty;

            try
            {
                await this.client.RecordErrorAsync(exception.Message, logId, exception.StackTrace);
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
                if (this.coreSettings.AnalyticsToken == null)
                {
                    string analyticsToken = await this.client.CreateUserAsync();
                    this.coreSettings.AnalyticsToken = analyticsToken;

                    await this.client.RecordDeviceInformationAsync();

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
        }

        private async Task<bool> AwaitAuthenticationAsync(bool force = false)
        {
            if (!coreSettings.EnableAutomaticReports && !force)
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

            if (this.isAuthenticated)
                return true;

            await this.isAuthenticating.FirstAsync(x => !x);

            return true;
        }

        private async Task<Stream> GetCompressedLogFileStreamAsync()
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Espera\Log.txt");

            try
            {
                using (FileStream stream = await Task.Run(() => File.OpenRead(logPath)))
                {
                    // Buddy doesn't like empty streams
                    if (stream.Length == 0)
                        return null;

                    using (var inputStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(inputStream);

                        var compressedStream = new MemoryStream();

                        using (var compressionStream = new GZipStream(compressedStream, CompressionMode.Compress, true))
                        {
                            inputStream.Position = 0;
                            await inputStream.CopyToAsync(compressionStream);
                        }
                        compressedStream.Position = 0;
                        return compressedStream;
                    }
                }
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't compress log file", ex);
                return null;
            }
        }

        private async Task<string> SendLogFileAsync()
        {
            Stream logFileSteam = await this.GetCompressedLogFileStreamAsync();

            if (logFileSteam == null)
                return null;

            using (logFileSteam)
            {
                try
                {
                    return await this.client.SendCrashLogAsync(logFileSteam);
                }

                catch (Exception ex)
                {
                    this.Log().ErrorException("Couldn't send log file", ex);
                    return null;
                }
            }
        }
    }
}