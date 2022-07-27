using System;
using System.Runtime.CompilerServices;

namespace Espera.Core
{
    public class MeasureHelper
    {
        public static IDisposable Measure([CallerMemberName] string caller = null)
        {
            var stopWatch = Stopwatch.StartNew();

            return Disposable.Create(() =>
            {
                stopWatch.Stop();
                Debug.WriteLine("Measured in {0}: {1}ms", caller, stopWatch.ElapsedMilliseconds);
                LogHost.Default.Debug("Measured in {0}: {1}ms", caller, stopWatch.ElapsedMilliseconds);
            });
        }
    }
}