using Buddy;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Espera.Core.Analytics
{
    public class BuddyAnalyticsEndpoint : IAnalyticsEndpoint
    {
        private readonly BuddyClient client;
        private AuthenticatedUser storedUser;

        public BuddyAnalyticsEndpoint()
        {
            this.client = new BuddyClient("Espera", "EC60C045-B432-44A6-A4E0-15B4BF607105", autoRecordDeviceInfo: false);
        }

        public async Task AuthenticateUserAsync(string analyticsToken)
        {
            this.storedUser = await this.client.LoginAsync(analyticsToken);
        }

        public async Task<string> CreateUserAsync()
        {
            string throwAwayToken = Guid.NewGuid().ToString();

            AuthenticatedUser user = await this.client.CreateUserAsync(throwAwayToken, throwAwayToken);

            this.storedUser = user;

            return user.Token;
        }

        public Task RecordDeviceInformationAsync()
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            return this.client.Device.RecordInformationAsync(Environment.OSVersion.VersionString, "Desktop", this.storedUser, version);
        }

        public Task RecordErrorAsync(string message, string logId, string stackTrace = null)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            return this.client.Device.RecordCrashAsync(message, Environment.OSVersion.VersionString, "Desktop", this.storedUser, stackTrace, version, metadata: logId);
        }

        public Task RecordMetaDataAsync(string key, string value)
        {
            return this.storedUser.Metadata.SetAsync(key, value);
        }

        public async Task<string> SendBlobAsync(string name, string mimeType, Stream data)
        {
            Blob blob = await this.storedUser.Blobs.AddAsync(name, mimeType, String.Empty, 0, 0, data);
            return blob.BlobID.ToString(CultureInfo.InvariantCulture);
        }

        public Task UpdateUserEmailAsync(string email)
        {
            AuthenticatedUser user = this.storedUser;

            return user.UpdateAsync(user.Email, String.Empty, user.Gender, user.Age, email, // email is the only field we change here
                user.Status, user.LocationFuzzing, user.CelebrityMode, user.ApplicationTag);
        }
    }
}