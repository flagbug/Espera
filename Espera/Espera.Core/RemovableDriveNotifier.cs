using System;
using System.Management;
using Rareform.Extensions;

namespace Espera.Core
{
    public class RemovableDriveNotifier : IDisposable
    {
        private ManagementEventWatcher removeWatcher;
        private ManagementEventWatcher insertWatcher;

        public event EventHandler DriveInserted;
        public event EventHandler DriveRemoved;

        private RemovableDriveNotifier()
        { }

        public static RemovableDriveNotifier Create()
        {
            var notifier = new RemovableDriveNotifier();
            notifier.StartInsertWatcher();
            notifier.StartRemoveWatcher();

            return notifier;
        }

        public void Dispose()
        {
            this.insertWatcher.Dispose();
            this.removeWatcher.Dispose();
        }

        private void StartRemoveWatcher()
        {
            var scope = new ManagementScope("root\\CIMV2") { Options = { EnablePrivileges = true } };

            var query = new WqlEventQuery
            {
                EventClassName = "__InstanceDeletionEvent",
                WithinInterval = TimeSpan.FromSeconds(3),
                Condition = "TargetInstance ISA 'Win32_USBControllerdevice'"
            };

            this.removeWatcher = new ManagementEventWatcher(scope, query);
            this.removeWatcher.EventArrived += (sender, e) => this.DriveRemoved.RaiseSafe(this, EventArgs.Empty);

            this.removeWatcher.Start();
        }

        private void StartInsertWatcher()
        {
            var scope = new ManagementScope("root\\CIMV2") { Options = { EnablePrivileges = true } };

            var query = new WqlEventQuery
            {
                EventClassName = "__InstanceCreationEvent",
                WithinInterval = TimeSpan.FromSeconds(3),
                Condition = "TargetInstance ISA 'Win32_USBControllerdevice'"
            };

            this.insertWatcher = new ManagementEventWatcher(scope, query);
            this.insertWatcher.EventArrived += (sender, e) => this.DriveInserted.RaiseSafe(this, EventArgs.Empty);

            this.insertWatcher.Start();
        }
    }
}