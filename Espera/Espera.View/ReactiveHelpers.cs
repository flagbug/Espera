using ReactiveMarrow;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Espera.View
{
    internal static class ReactiveHelpers
    {
        public static IObservable<TLeft> WithWhere<TLeft, TRight>(this IObservable<TLeft> left, IObservable<TRight> right, Func<TRight, bool> predicate)
        {
            TRight latest = default(TRight);
            bool initialized = false;

            var disp = new CompositeDisposable(2);

            right.Subscribe(x =>
            {
                latest = x;
                initialized = true;
            }).DisposeWith(disp);

            return Observable.Create<TLeft>(o =>
            {
                left.Subscribe(x =>
                {
                    if (initialized && predicate(latest))
                    {
                        o.OnNext(x);
                    }
                }, o.OnError, o.OnCompleted).DisposeWith(disp);

                return disp;
            });
        }
    }
}