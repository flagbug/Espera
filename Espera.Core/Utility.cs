using System;
using System.Collections.Specialized;
using System.Reactive.Linq;

namespace Espera.Core
{
    public static class Utility
    {
        public static IObservable<NotifyCollectionChangedEventArgs> Changed<T>(this T source)
            where T : class, INotifyCollectionChanged
        {
            if (source == null)
                throw new ArgumentNullException("source");

            return Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                handler => source.CollectionChanged += handler,
                handler => source.CollectionChanged -= handler)
                .Select(x => x.EventArgs);
        }

        public static void Retry(this Action block, int retries = 3)
        {
            while (true)
            {
                try
                {
                    block();
                    return;
                }

                catch (Exception)
                {
                    retries--;

                    if (retries == 0)
                    {
                        throw;
                    }
                }
            }
        }
    }
}