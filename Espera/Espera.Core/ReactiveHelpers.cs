using Rareform.Validation;
using ReactiveMarrow;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Espera.Core
{
    public static class ReactiveHelpers
    {
        /// <summary>
        /// Takes the left observable and combines it with the latest value of the right observable.
        /// This method is like <see cref="Observable.CombineLatest{TSource1,TSource2,TResult}"/>,
        /// except it propagates only when the value of the left observable sequence changes.
        /// </summary>
        public static IObservable<TResult> CombineLatestValue<TLeft, TRight, TResult>(this IObservable<TLeft> left, IObservable<TRight> right, Func<TLeft, TRight, TResult> resultSelector)
        {
            if (left == null)
                Throw.ArgumentNullException(() => left);

            if (right == null)
                Throw.ArgumentNullException(() => right);

            if (resultSelector == null)
                Throw.ArgumentNullException(() => resultSelector);

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
                left.Where(_ => initialized)
                    .Select(x => resultSelector(x, latest))
                    .Subscribe(o)
                    .DisposeWith(disp);

                return disp;
            });
        }
    }
}