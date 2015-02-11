using System;
using System.Collections.Generic;
using System.Reactive.Disposables;

namespace Espera.Core.Analytics
{
    internal class DummyAnalyticsEndpoint : IAnalyticsEndpoint
    {
        public void Dispose()
        {
        }

        public void Identify(IDictionary<string, string> traits = null)
        {
        }

        public void Initialize()
        {
        }

        public void ReportBug(string message)
        {
        }

        public void ReportFatalException(Exception exception)
        {
        }

        public void ReportNonFatalException(Exception exception)
        {
        }

        public void Track(string key, IDictionary<string, string> traits = null)
        {
        }

        public IDisposable TrackTime(string key, IDictionary<string, string> traits = null)
        {
            return Disposable.Empty;
        }

        public void UpdateEmail(string email)
        {
        }
    }
}