using System;

namespace Espera.Services
{
    /// <summary>
    /// Represents stats about the current stats of the mobile API, the number of currently
    /// connected clients for example.
    /// </summary>
    public class MobileApiInfo
    {
        public MobileApiInfo(IObservable<int> connectedClientCount, IObservable<bool> isPortOccupied)
        {
            if (connectedClientCount == null)
                throw new ArgumentNullException("connectedClientCount");

            if (isPortOccupied == null)
                throw new ArgumentNullException("isPortOccupied");

            this.ConnectedClientCount = connectedClientCount;
            this.IsPortOccupied = isPortOccupied;
        }

        public IObservable<int> ConnectedClientCount { get; private set; }

        public IObservable<bool> IsPortOccupied { get; private set; }
    }
}