using System;

namespace Espera.Services
{
    /// <summary>
    /// Represents stats about the current stats of the mobile API, the number of currently
    /// connected clients for example.
    /// </summary>
    public class MobileApiInfo
    {
        public MobileApiInfo(IObservable<int> connectedClientCount)
        {
            if (connectedClientCount == null)
                throw new ArgumentNullException("connectedClientCount");

            this.ConnectedClientCount = connectedClientCount;
        }

        public IObservable<int> ConnectedClientCount { get; private set; }
    }
}