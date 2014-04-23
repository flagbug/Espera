using System;
using System.Net.NetworkInformation;
using System.Reactive.Linq;

namespace Espera.View
{
    public interface INetworkStatus
    {
        /// <summary>
        /// Returns an observable that returns the current network status.
        /// This observable returns the current status upon subscription.
        /// </summary>
        IObservable<bool> IsAvailable { get; }
    }

    public class NetworkStatus : INetworkStatus
    {
        public NetworkStatus()
        {
            this.IsAvailable = Observable.FromEventPattern<NetworkAvailabilityChangedEventHandler, NetworkAvailabilityEventArgs>(
                h => NetworkChange.NetworkAvailabilityChanged += h,
                h => NetworkChange.NetworkAvailabilityChanged -= h)
                .Select(x => x.EventArgs.IsAvailable)
                .StartWith(NetworkInterface.GetIsNetworkAvailable());
        }

        public IObservable<bool> IsAvailable { get; private set; }
    }
}