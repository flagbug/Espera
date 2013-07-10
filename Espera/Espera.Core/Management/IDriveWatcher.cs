using System;
using System.Reactive;

namespace Espera.Core.Management
{
    public interface IRemovableDriveWatcher : IDisposable
    {
        IObservable<Unit> DriveRemoved { get; }

        void Initialize();
    }
}