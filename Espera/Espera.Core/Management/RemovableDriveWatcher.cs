using System;
using System.Reactive;
using System.Reactive.Linq;

namespace Espera.Core.Management
{
    public class RemovableDriveWatcher : IRemovableDriveWatcher
    {
        private Rareform.IO.RemovableDriveWatcher driveWatcher;

        public IObservable<Unit> DriveRemoved { get; private set; }

        public void Dispose()
        {
            this.driveWatcher.Dispose();
        }

        public void Initialize()
        {
            this.driveWatcher = Rareform.IO.RemovableDriveWatcher.Create();

            this.DriveRemoved = Observable.FromEvent<EventHandler, EventArgs>(
                handler => this.driveWatcher.DriveRemoved += handler,
                handler => this.driveWatcher.DriveRemoved -= handler)
                .Select(_ => Unit.Default)
                .AsObservable();
        }
    }
}