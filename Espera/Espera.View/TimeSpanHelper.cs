using System;

namespace Espera.View
{
    public static class TimeSpanHelper
    {
        /// <summary>
        /// Formats a <see cref="TimeSpan" /> object adaptively, so that the hour part is only shown
        /// when needed.
        /// </summary>
        public static string FormatAdaptive(this TimeSpan timeSpan)
        {
            string formatted = timeSpan.ToString("mm\\:ss");

            if (timeSpan.Hours == 0)
                return formatted;

            return timeSpan.ToString("hh\\:") + formatted;
        }
    }
}