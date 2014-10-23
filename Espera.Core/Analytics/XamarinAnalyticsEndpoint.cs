using System;
using System.Collections.Generic;
using Xamarin;

namespace Espera.Core.Analytics
{
    internal class XamarinAnalyticsEndpoint : IAnalyticsEndpoint
    {
        private Guid id;

        public void Dispose()
        {
            // Xamarin Insights can only be terminated if it has been started before, otherwise it
            // throws an exception
            if (Insights.IsInitialized)
            {
                Insights.Terminate();
            }
        }

        public void Identify(string id, IDictionary<string, string> traits = null)
        {
            traits.Add(Insights.Traits.Name, id);

            Insights.Identify(id, traits);
        }

        public void Initialize(Guid id)
        {
            this.id = id;

            Insights.Initialize("ed4fea5ffb4fa2a1d36acfeb3df4203153d92acf", AppInfo.Version.ToString(), "Espera", AppInfo.BlobCachePath);
        }

        public void ReportBug(string message)
        {
            var exception = new Exception(message);

            Insights.Report(exception);
        }

        public void ReportFatalException(Exception exception)
        {
            Insights.Report(exception, ReportSeverity.Error);
        }

        public void ReportNonFatalException(Exception exception)
        {
            Insights.Report(exception);
        }

        public void Track(string key, IDictionary<string, string> traits = null)
        {
            Insights.Track(key, traits);
        }

        public IDisposable TrackTime(string key, IDictionary<string, string> traits = null)
        {
            return Insights.TrackTime(key, traits);
        }

        public void UpdateEmail(string email)
        {
            Insights.Identify(id.ToString(), Insights.Traits.Email, email);
        }
    }
}