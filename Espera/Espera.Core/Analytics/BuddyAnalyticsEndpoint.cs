using System;
using System.IO;
using System.Threading.Tasks;
using BuddySDK;

namespace Espera.Core.Analytics
{
    public class BuddyAnalyticsEndpoint : IAnalyticsEndpoint
    {
        private readonly BuddyClient client;
        private AuthenticatedUser storedUser;

        public BuddyAnalyticsEndpoint()
        {
            this.client = new BuddyClient("bbbbbc.mhbbbxjLrKNl", "83585740-AE7A-4F68-828D-5E6A8825A0EE");
        }

        public async Task AuthenticateUserAsync(string analyticsToken)
        {
            var result = await this.client.LoginUserAsync(analyticsToken, analyticsToken);

            if (!result.IsSuccess)
            {
                throw result.Error;
            }

            this.storedUser = result.Value;
        }

        public async Task<string> CreateUserAsync()
        {
            string token = Guid.NewGuid().ToString();

            var result = await this.client.CreateUserAsync(token, token);

            if (!result.IsSuccess)
            {
                throw result.Error;
            }

            this.storedUser = result.Value;

            return token;
        }

        public async Task RecordDeviceInformationAsync()
        {
            await this.RecordLanguageAsync();
        }

        public async Task RecordErrorAsync(Exception ex, string message = null)
        {
            var result = await this.client.AddCrashReportAsync(ex, message);

            if (!result.IsSuccess)
            {
                throw result.Error;
            }
        }

        public async Task RecordMetaDataAsync(string key, string value)
        {
            var result = await this.storedUser.SetMetadataAsync(key, value);

            if (!result.IsSuccess)
            {
                throw result.Error;
            }
        }

        public async Task<string> SendBlobAsync(string name, string mimeType, Stream data)
        {
            return null;
        }

        public async Task UpdateUserEmailAsync(string email)
        {
            AuthenticatedUser user = this.storedUser;

            user.Email = email;

            var result = await user.SaveAsync();

            if (!result.IsSuccess)
            {
                throw result.Error;
            }
        }
    }
}