using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Espera.Core.Analytics
{
    public interface IAnalyticsEndpoint
    {
        Task AuthenticateUserAsync(string analyticsToken);

        /// <summary>
        /// Creates a new user and returns a unique authentication token. This method also
        /// authenticates the with the returned token.
        /// </summary>
        /// <returns>A unique token used for the next authentication.</returns>
        Task<string> CreateUserAsync();

        /// <summary>
        /// Records runtime information about this device and application.
        /// </summary>
        Task RecordDeviceInformationAsync();

        Task RecordErrorAsync(Exception ex, string message = null);

        Task RecordMetaDataAsync(string key, string value);

        Task UpdateUserEmailAsync(string email);
    }

    public static class AnalyticsEndpointMixin
    {
        public static Task RecordLanguageAsync(this IAnalyticsEndpoint endpoint)
        {
            return endpoint.RecordMetaDataAsync("language", CultureInfo.InstalledUICulture.TwoLetterISOLanguageName);
        }

        public static Task RecordLibrarySizeAsync(this IAnalyticsEndpoint endpoint, int songCount)
        {
            return endpoint.RecordMetaDataAsync("library-size", songCount.ToString(CultureInfo.InvariantCulture));
        }

        public static Task RecordMobileUsageAsync(this IAnalyticsEndpoint endpoint)
        {
            return endpoint.RecordMetaDataAsync("uses-mobile", "true");
        }
    }
}