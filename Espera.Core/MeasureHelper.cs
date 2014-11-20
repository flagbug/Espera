using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using Splat;

namespace Espera.Core
{
    public static class MeasureHelper
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