using System;
using System.Collections.Generic;
using Xamarin;

namespace Espera.Core.Analytics
{
    internal class XamarinAnalyticsEndpoint : IAnalyticsEndpoint
    {
        private bool isInitialized;

        public void Dispose()
        {
            // Xamarin Insights can only be terminated if it has been started before, otherwise it
            // throws an exception
            if (this.isInitialized)
            {
                Insights.Terminate();
                this.isInitialized = false;
            }
        }

        public void Identify(string id, IDictionary<string, string> traits = null)
        {
            Insights.Identify(id, traits);
        }

        public void Initialize()
        {
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

        public void UpdateEmail(string email)
        {
            throw new NotImplementedException();
        }
    }
}