using System;
using System.Collections.Generic;

namespace Espera.Core.Analytics
{
    internal class DummyAnalyticsEndpoint : IAnalyticsEndpoint
    {
        public void Dispose()
        {
        }

        public void Identify(string id, IDictionary<string, string> traits = null)
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

        public void UpdateEmail(string email)
        {
        }
    }
}