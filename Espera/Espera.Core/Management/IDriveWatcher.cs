using System;

namespace Espera.Core.Management
{
    public interface IRemovableDriveWatcher : IDisposable
    {
        event EventHandler DriveRemoved;
    }
}