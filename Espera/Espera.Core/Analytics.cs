using Buddy;
using Espera.Core.Settings;
using ReactiveUI;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading.Tasks;

namespace Espera.Core
{
    public class Analytics : IEnableLogger
    {
        private static readonly Lazy<Analytics> instance;
        private readonly BehaviorSubject<bool> isAuthenticating;
        private BuddyClient client;
        private bool isAuthenticated;
        private AuthenticatedUser user;

        static Analytics()
        {
            instance = new Lazy<Analytics>(() => new Analytics());
        }

        public Analytics()
        {
            this.isAuthenticating = new BehaviorSubject<bool>(false);
        }

        public static Analytics Instance
        {
            get { return instance.Value; }
        }

        public async Task InitializeAsync(CoreSettings settings)
        {
            this.isAuthenticating.OnNext(true);

            this.client = new BuddyClient("Espera", "EC60C045-B432-44A6-A4E0-15B4BF607105");

            try
            {
                if (settings.AnalyticsToken == null)
                {
                    string throwAwayToken = Guid.NewGuid().ToString(); // A token that we immediately throw away because we don't need it
                    this.user = await this.client.CreateUserAsync(throwAwayToken, throwAwayToken);
                    settings.AnalyticsToken = this.user.Token;

                    string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    await this.client.Device.RecordInformationAsync(Environment.OSVersion.VersionString, "Desktop", this.user, version);

                    this.Log().Info("Created new analytics user");
                }

                else
                {
                    this.user = await this.client.LoginAsync(settings.AnalyticsToken);

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

        public async Task<bool> RecordBugReportAsync(string message, string email)
        {
            await this.AwaitAuthenticationAsync();

            if (!this.isAuthenticated)
                return false;

            if (!String.IsNullOrWhiteSpace(email))
            {
                await UpdateUserEmailAsync(this.user, email);
            }

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            string logId = await this.SendLogFileAsync();

            await this.client.Device.RecordCrashAsync(message, Environment.OSVersion.VersionString, "Desktop", this.user, null, version, metadata: logId);

            return true;
        }

        public async Task<bool> RecordCrashAsync(Exception exception, string email)
        {
            await this.AwaitAuthenticationAsync();

            if (!this.isAuthenticated)
                return false;

            if (!String.IsNullOrWhiteSpace(email))
            {
                await UpdateUserEmailAsync(this.user, email);
            }

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            string logId = await this.SendLogFileAsync();

            await this.client.Device.RecordCrashAsync(exception.Message, Environment.OSVersion.VersionString,
                "Desktop", this.user, exception.StackTrace, version, metadata: logId);

            return true;
        }

        public async Task RecordLibrarySizeAsync(int songCount)
        {
            await this.AwaitAuthenticationAsync();

            if (!this.isAuthenticated)
                return;

            try
            {
                await this.user.Metadata.SetAsync("library-size", songCount.ToString(CultureInfo.InvariantCulture));
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Could not log library size", ex);
            }
        }

        public async Task RecordMobileUsage()
        {
            await this.AwaitAuthenticationAsync();

            if (!this.isAuthenticated)
                return;

            try
            {
                await this.user.Metadata.SetAsync("uses-mobile", "true");
            }

            catch (Exception ex)
            {
                this.Log().InfoException("Could not log mobile usage", ex);
            }
        }

        private static async Task UpdateUserEmailAsync(AuthenticatedUser user, string email)
        {
            await user.UpdateAsync(user.Email, String.Empty, user.Gender, user.Age, email, // email is the only field we change here
                user.Status, user.LocationFuzzing, user.CelebrityMode, user.ApplicationTag);
        }

        private async Task AwaitAuthenticationAsync()
        {
            if (this.isAuthenticated)
                return;

            var finished = this.isAuthenticating.FirstAsync(x => !x).ToTask();

            await finished;
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
                    Blob blob = await this.user.Blobs.AddAsync("Crash report log", "application/zip", String.Empty, 0, 0, logFileSteam);
                    return blob.BlobID.ToString(CultureInfo.InvariantCulture);
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