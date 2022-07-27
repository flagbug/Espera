using System;
using System.Collections.Generic;

namespace Espera.Core.Mobile
{
    /// <summary>
    ///     Represents stats about the current stats of the mobile API, the number of currently
    ///     connected clients for example.
    /// </summary>
    public class MobileApiInfo
    {
        public MobileApiInfo(IObservable<IReadOnlyList<MobileClient>> connectedClients,
            IObservable<bool> isPortOccupied)
        {
            if (connectedClients == null)
                throw new ArgumentNullException("connectedClients");

            if (isPortOccupied == null)
                throw new ArgumentNullException("isPortOccupied");

            IsPortOccupied = isPortOccupied;

            VideoPlayerToggleRequest = connectedClients.Select(x => x.Select(y => y.VideoPlayerToggleRequest).Merge())
                .Switch();
            ConnectedClientCount = connectedClients.Select(x => x.Count);
        }

        public IObservable<int> ConnectedClientCount { get; }

        public IObservable<bool> IsPortOccupied { get; }

        public IObservable<Unit> VideoPlayerToggleRequest { get; }
    }
}