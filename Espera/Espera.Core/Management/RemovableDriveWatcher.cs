using System;

namespace Espera.Core.Management
{
    public class RemovableDriveWatcher : IRemovableDriveWatcher
    {
        private readonly Rareform.IO.RemovableDriveWatcher driveWatcher;

        public RemovableDriveWatcher()
        {
            driveWatcher = Rareform.IO.RemovableDriveWatcher.Create();
        }

        public void Dispose()
        {
            this.driveWatcher.Dispose();
        }

        public event EventHandler DriveRemoved
        {
            add
            {
                this.driveWatcher.DriveRemoved += value;
            }

            remove
            {
                this.driveWatcher.DriveRemoved -= value;
            }
        }
    }
}