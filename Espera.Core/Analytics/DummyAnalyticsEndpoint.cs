using System;
using System.Collections.Generic;
using System.Reactive.Disposables;

namespace Espera.Core.Analytics
{
    internal class DummyAnalyticsEndpoint : IAnalyticsEndpoint
    {
        public void Dispose()
        { }

        public void Identify(string id, IDictionary<string, string> traits = null)
        { }

        public void Initialize(Guid id)
        { }

        public void ReportBug(string message)
        { }

        public void ReportFatalException(Exception exception)
        { }

        public void ReportNonFatalException(Exception exception)
        { }

        public void Track(string key, IDictionary<string, string> traits = null)
        { }

        public IDisposable TrackTime(string key, IDictionary<string, string> traits = null) => Disposable.Empty;

        public void UpdateEmail(string email)
        { }
    }
}