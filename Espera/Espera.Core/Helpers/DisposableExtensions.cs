using System;
using System.Reactive.Disposables;

namespace Espera.Core.Helpers
{
    public static class DisposableExtensions
    {
        public static T DisposeWith<T>(this T disposable, CompositeDisposable with) where T : IDisposable
        {
            with.Add(disposable);
            return disposable;
        }
    }
}