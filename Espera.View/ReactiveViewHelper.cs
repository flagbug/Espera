using System;
using System.Reactive.Linq;
using System.Windows;

namespace Espera.View
{
    public static class ReactiveViewHelper
    {
        public static IObservable<Tuple<object, T>> RegisterEventSetter<T>(this Style style, RoutedEvent routedEvent, Func<EventHandler<T>, Delegate> eventHandlerCreator)
            where T : EventArgs
        {
            return Observable.Create<Tuple<object, T>>(o =>
            {
                var handler = eventHandlerCreator((sender, args) => o.OnNext(Tuple.Create(sender, args)));
                var eventSetter = new EventSetter(routedEvent, handler);

                style.Setters.Add(eventSetter);

                // We can't remove the setter anymore as the setter collection gets sealed
                return () => { };
            });
        }
    }
}