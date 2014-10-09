using System;
using System.Collections.Generic;

namespace Espera.Core.Analytics
{
    public interface IAnalyticsEndpoint : IDisposable
    {
        void Identify(string id, IDictionary<string, string> traits = null);

        void Initialize(Guid id);

        void ReportBug(string message);

        void ReportFatalException(Exception exception);

        void ReportNonFatalException(Exception exception);

        void Track(string key, IDictionary<string, string> traits = null);

        IDisposable TrackTime(string key, IDictionary<string, string> traits = null);

        void UpdateEmail(string email);
    }
}