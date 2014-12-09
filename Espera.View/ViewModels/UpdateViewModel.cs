using Akavache;
using Espera.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Splat;
using Squirrel;
using System.Linq;
using Espera.Core.Analytics;
using System.Reactive.Threading.Tasks;

namespace Espera.View.ViewModels
{
    public class UpdateViewModel : ReactiveObject, IDisposable
    {
        private readonly ViewSettings settings;
        private readonly ObservableAsPropertyHelper<bool> shouldRestart;
        private readonly IUpdateManager updateManager;

        public UpdateViewModel(ViewSettings settings, IUpdateManager updateManager = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            this.settings = settings;
            this.updateManager = updateManager ?? new UpdateManager(AppInfo.UpdatePath, "Espera", FrameworkVersion.Net45, AppInfo.AppRootPath);

            this.CheckForUpdate = ReactiveCommand.CreateAsyncTask(_ => this.UpdateSilentlyAsync());

            this.shouldRestart = this.settings.WhenAnyValue(x => x.IsUpdated)
                .ToProperty(this, x => x.ShouldRestart);

            this.Restart = ReactiveCommand.CreateAsyncTask(_ => Task.Run(() => UpdateManager.RestartApp()));

            Observable.Interval(TimeSpan.FromHours(2), RxApp.TaskpoolScheduler)
                .StartWith(0) // Trigger an initial update check
                .InvokeCommand(this.CheckForUpdate);
        }

        /// <summary>
        /// Checks the server if an update is available and applies the update if there is one.
        /// </summary>
        public ReactiveCommand<Unit> CheckForUpdate { get; }

        /// <summary>
        /// Used in the changelog dialog to opt-out of the automatic changelog.
        /// </summary>
        public bool DisableChangelog { get; set; }

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

        public bool ShouldRestart => this.shouldRestart.Value;

        public bool ShowChangelog => this.settings.IsUpdated && this.settings.EnableChangelog;

        public void ChangelogShown()
        {
            this.settings.IsUpdated = false;

            this.settings.EnableChangelog = !this.DisableChangelog;
        }

        public void Dispose()
        {
            this.updateManager.Dispose();
        }

        private async Task UpdateSilentlyAsync()
        {
            if (ModeDetector.InUnitTestRunner())
                return;

            this.Log().Info("Looking for application updates");

            UpdateInfo updateInfo;

            try
            {
                updateInfo = await this.updateManager.CheckForUpdate();
            }

            catch (Exception ex)
            {
                this.Log().ErrorException("Error while checking for updates", ex);
                return;
            }

            if (updateInfo.ReleasesToApply.Any())
            {
                this.Log().Info("New version available: {0}", updateInfo.FutureReleaseEntry.Version);

                Task changelogFetchTask = ChangelogFetcher.FetchAsync().ToObservable()
                    .Timeout(TimeSpan.FromSeconds(30))
                    .SelectMany(x => BlobCache.LocalMachine.InsertObject(BlobCacheKeys.Changelog, x))
                    .LoggedCatch(this, Observable.Return(Unit.Default), "Could not to fetch changelog")
                    .ToTask();

                this.Log().Info("Downloading updates...");

                try
                {
                    await this.updateManager.DownloadReleases(updateInfo.ReleasesToApply);
                }

                catch (Exception ex)
                {
                    this.Log().Error("Failed to download updates.", ex);
                    AnalyticsClient.Instance.RecordNonFatalError(ex);
                    return;
                }

                this.Log().Info("Applying updates...");

                try
                {
                    await this.updateManager.ApplyReleases(updateInfo);
                }

                catch (Exception ex)
                {
                    this.Log().Error("Failed to apply updates.", ex);
                    AnalyticsClient.Instance.RecordNonFatalError(ex);
                    return;
                }

                await changelogFetchTask;

                this.settings.IsUpdated = true;

                this.Log().Info("Updates applied.");
            }

            else
            {
                this.Log().Info("No updates found");
            }
        }
    }
}