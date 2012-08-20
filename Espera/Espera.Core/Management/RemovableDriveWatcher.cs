using System;

namespace Espera.Core.Management
{
    public class RemovableDriveWatcher : IRemovableDriveWatcher
    {
        private Rareform.IO.RemovableDriveWatcher driveWatcher;

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

        public void Dispose()
        {
            this.driveWatcher.Dispose();
        }

        public void Initialize()
        {
            this.driveWatcher = Rareform.IO.RemovableDriveWatcher.Create();
        }
    }
}