using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace Espera.Core.Mobile
{
    /// <summary>
    /// Represents stats about the current stats of the mobile API, the number of currently
    /// connected clients for example.
    /// </summary>
    public class MobileApiInfo
    {
        public MobileApiInfo(IObservable<IReadOnlyList<MobileClient>> connectedClients, IObservable<bool> isPortOccupied)
        {
            if (connectedClients == null)
                throw new ArgumentNullException("connectedClients");

            if (isPortOccupied == null)
                throw new ArgumentNullException("isPortOccupied");

            this.IsPortOccupied = isPortOccupied;

            this.VideoPlayerToggleRequest = connectedClients.Select(x => x.Select(y => y.VideoPlayerToggleRequest).Merge()).Switch();
            this.ConnectedClientCount = connectedClients.Select(x => x.Count);
        }

        public IObservable<int> ConnectedClientCount { get; private set; }

        public IObservable<bool> IsPortOccupied { get; private set; }

        public IObservable<Unit> VideoPlayerToggleRequest { get; private set; }
    }
}