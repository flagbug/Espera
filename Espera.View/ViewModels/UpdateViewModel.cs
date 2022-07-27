using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Analytics;

namespace Espera.View.ViewModels
{
    public class UpdateViewModel : ReactiveObject, IDisposable
    {
        private readonly ViewSettings settings;
        private readonly ObservableAsPropertyHelper<bool> shouldRestart;
        private readonly object updateLock;
        private readonly IUpdateManager updateManager;
        private bool updateRun;

        public UpdateViewModel(ViewSettings settings, IUpdateManager updateManager = null)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.settings = settings;
            this.updateManager =
                updateManager ?? new UpdateManager(AppInfo.UpdatePath, "Espera", FrameworkVersion.Net45);

            updateLock = new object();

            CheckForUpdate = ReactiveCommand.CreateAsyncTask(_ => UpdateSilentlyAsync());

            shouldRestart = this.settings.WhenAnyValue(x => x.IsUpdated)
                .ToProperty(this, x => x.ShouldRestart);

            Restart = ReactiveCommand.CreateAsyncTask(_ => Task.Run(() => UpdateManager.RestartApp()));

            Observable.Interval(TimeSpan.FromHours(2), RxApp.TaskpoolScheduler)
                .StartWith(0) // Trigger an initial update check
                .InvokeCommand(CheckForUpdate);
        }

        /// <summary>
        ///     Checks the server if an update is available and applies the update if there is one.
        /// </summary>
        public ReactiveCommand<Unit> CheckForUpdate { get; }

        /// <summary>
        ///     Used in the changelog dialog to opt-out of the automatic changelog.
        /// </summary>
        public bool DisableChangelog { get; set; }

        public string PortableDownloadLink => "http://getespera.com/EsperaPortable.zip";

        public IEnumerable<ChangelogReleaseEntry> ReleaseEntries
        {
            get
            {
                return BlobCache.LocalMachine.GetObject<Changelog>(BlobCacheKeys.Changelog)
                    .Select(x => x.Releases)
                    .Wait();
            }
        }

        public ReactiveCommand<Unit> Restart { get; }

        public bool ShouldRestart => shouldRestart.Value;

        public bool ShowChangelog => settings.IsUpdated && settings.EnableChangelog;

        public void Dispose()
        {
            updateManager.Dispose();
        }

        public void ChangelogShown()
        {
            settings.EnableChangelog = !DisableChangelog;
        }

        public void DismissUpdateNotification()
        {
            // We don't want to overwrite the update status if the update manager already downloaded
            // the update
            lock (updateLock)
            {
                if (!updateRun) settings.IsUpdated = false;
            }
        }

        private async Task UpdateSilentlyAsync()
        {
            if (ModeDetector.InUnitTestRunner())
                return;
#if DEBUG
            return;
#endif

            ReleaseEntry appliedEntry;

            try
            {
                appliedEntry = await updateManager.UpdateApp();
            }

            catch (Exception ex)
            {
                this.Log().Error("Failed to update application", ex);
                AnalyticsClient.Instance.RecordNonFatalError(ex);
                return;
            }

            if (appliedEntry == null)
            {
                this.Log().Info("No update available");
                return;
            }

            await ChangelogFetcher.FetchAsync().ToObservable()
                .Timeout(TimeSpan.FromSeconds(30))
                .SelectMany(x => BlobCache.LocalMachine.InsertObject(BlobCacheKeys.Changelog, x))
                .LoggedCatch(this, Observable.Return(Unit.Default), "Could not to fetch changelog")
                .ToTask();

            lock (updateLock)
            {
                updateRun = true;
                settings.IsUpdated = true;
            }

            this.Log().Info("Updated to version {0}", appliedEntry.Version);
        }
    }
}