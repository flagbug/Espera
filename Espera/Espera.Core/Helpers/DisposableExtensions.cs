using System;
using System.Reactive.Disposables;

namespace Espera.Core.Helpers
{
    public static class DisposableExtensions
    {
        public static void DisposeWith(this IDisposable disposable, CompositeDisposable with)
        {
            with.Add(disposable);
        }
    }
}