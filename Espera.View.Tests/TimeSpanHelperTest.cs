using System;
using Xunit;

namespace Espera.View.Tests
{
    public class TimeSpanHelperTest
    {
        [Fact]
        public void FormatAdaptiveIncludesHourIfHourIsGreaterThanZero()
        {
            var timeSpan = new TimeSpan(0, 3, 5, 10);

            string formatted = timeSpan.FormatAdaptive();

            Assert.Equal("03:05:10", formatted);
        }

        [Fact]
        public void FormatAdaptiveStripsHourIfHourIsZero()
        {
            var timeSpan = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(10));

            string formatted = timeSpan.FormatAdaptive();

            Assert.Equal("05:10", formatted);
        }
    }
}