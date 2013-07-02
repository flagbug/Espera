using ReactiveMarrow;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Espera.Core
{
    internal static class ReactiveHelpers
    {
        public static IObservable<TResult> Left<TLeft, TRight, TResult>(this IObservable<TLeft> left, IObservable<TRight> right, Func<TLeft, TRight, TResult> resultSelector)
        {
            TRight latest = default(TRight);
            bool initialized = false;

            var disp = new CompositeDisposable(2);

            right.Subscribe(x =>
            {
                latest = x;
                initialized = true;
            }).DisposeWith(disp);

            return Observable.Create<TResult>(o =>
            {
                left.Subscribe(x =>
                {
                    if (initialized)
                    {
                        o.OnNext(resultSelector(x, latest));
                    }
                }, o.OnError, o.OnCompleted).DisposeWith(disp);

                return disp;
            });
        }
    }
}