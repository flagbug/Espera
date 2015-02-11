using Akavache;
using Akavache.Sqlite3;
using Caliburn.Micro;
using Espera.Core;
using Espera.Core.Analytics;
using Espera.Core.Management;
using Espera.Core.Mobile;
using Espera.Core.Settings;
using Espera.View.CacheMigration;
using Espera.View.ViewModels;
using NLog.Config;
using NLog.Targets;
using ReactiveUI;
using Splat;
using Squirrel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Threading;

namespace Espera.View
{
    internal class AppBootstrapper : BootstrapperBase, IEnableLogger
    {
        private CoreSettings coreSettings;
        private MobileApi mobileApi;
        private ViewSettings viewSettings;

        static AppBootstrapper()
        {
            BlobCache.ApplicationName = "Espera";
        }

        public AppBootstrapper()
        {
            using (var mgr = new UpdateManager(AppInfo.UpdatePath, "Espera", FrameworkVersion.Net45))
            {
                // We have to re-implement the things Squirrel does for normal applications, because
                // we're marked as Squirrel-aware
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: v => mgr.CreateShortcutForThisExe(),
                    onAppUpdate: v =>
                    {
                        mgr.CreateShortcutForThisExe();
                        // Update the shortcut for the portable version
                        mgr.CreateShortcutsForExecutable("Espera.exe", ShortcutLocation.AppRoot, false);
                    },
                    onAppUninstall: v => mgr.RemoveShortcutForThisExe());
            }

            this.Initialize();
        }

        protected override void Configure()
        {
            this.viewSettings = new ViewSettings();
            Locator.CurrentMutable.RegisterConstant(this.viewSettings, typeof(ViewSettings));

            this.coreSettings = new CoreSettings();

            Locator.CurrentMutable.RegisterLazySingleton(() => new Library(new LibraryFileReader(AppInfo.LibraryFilePath),
                new LibraryFileWriter(AppInfo.LibraryFilePath), this.coreSettings, new FileSystem()), typeof(Library));

            Locator.CurrentMutable.RegisterLazySingleton(() => new WindowManager(), typeof(IWindowManager));

            Locator.CurrentMutable.RegisterLazySingleton(() => new SQLitePersistentBlobCache(Path.Combine(AppInfo.BlobCachePath, "api-requests.cache.db")),
                typeof(IBlobCache), BlobCacheKeys.RequestCacheContract);

            Locator.CurrentMutable.RegisterLazySingleton(() =>
                new ShellViewModel(Locator.Current.GetService<Library>(),
                    this.viewSettings, this.coreSettings,
                    Locator.Current.GetService<IWindowManager>(),
                    Locator.Current.GetService<MobileApiInfo>()),
                typeof(ShellViewModel));

            this.ConfigureLogging();
        }

        protected override IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return Locator.Current.GetServices(serviceType);
        }

        protected override object GetInstance(Type serviceType, string key)
        {
            return Locator.Current.GetService(serviceType, key);
        }

        protected override void OnExit(object sender, EventArgs e)
        {
            this.Log().Info("Starting Espera shutdown");

            this.Log().Info("Shutting down the library");
            Locator.Current.GetService<Library>().Dispose();

            this.Log().Info("Shutting down BlobCaches");
            BlobCache.Shutdown().Wait();
            var requestCache = Locator.Current.GetService<IBlobCache>(BlobCacheKeys.RequestCacheContract);
            requestCache.InvalidateAll().Wait();
            requestCache.Dispose();
            requestCache.Shutdown.Wait();

            this.Log().Info("Shutting down NLog");
            NLog.LogManager.Shutdown();

            if (this.mobileApi != null)
            {
                this.Log().Info("Shutting down mobile API");
                this.mobileApi.Dispose();
            }

            this.Log().Info("Shutting down analytics client");
            AnalyticsClient.Instance.Dispose();

            this.Log().Info("Shutdown finished");
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            this.Log().Info("Espera is starting...");
            this.Log().Info("******************************");
            this.Log().Info("**                          **");
            this.Log().Info("**          Espera          **");
            this.Log().Info("**                          **");
            this.Log().Info("******************************");
            this.Log().Info("Application version: " + AppInfo.Version);
            this.Log().Info("OS Version: " + Environment.OSVersion.VersionString);
            this.Log().Info("Current culture: " + CultureInfo.InstalledUICulture.Name);

            Directory.CreateDirectory(AppInfo.ApplicationRootPath);
            Directory.CreateDirectory(AppInfo.BlobCachePath);
            BlobCache.LocalMachine = new SQLitePersistentBlobCache(Path.Combine(AppInfo.BlobCachePath, "blobs.db"));

            var newBlobCache = BlobCache.LocalMachine;

            if (AkavacheToSqlite3Migration.NeedsMigration(newBlobCache))
            {
                var oldBlobCache = new DeprecatedBlobCache(AppInfo.BlobCachePath);
                var migration = new AkavacheToSqlite3Migration(oldBlobCache, newBlobCache);

                migration.Run();

                this.Log().Info("Removing all items from old BlobCache");
                oldBlobCache.InvalidateAll().Wait();

                this.Log().Info("Shutting down old BlobCache");
                oldBlobCache.Dispose();
                this.Log().Info("BlobCache shutdown finished");
            }

            this.SetupLager();

            this.SetupAnalyticsClient();

            this.SetupMobileApi();

            this.DisplayRootViewFor<ShellViewModel>();
        }

        protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
                return;

            this.Log().FatalException("An unhandled exception occurred, opening the crash report", e.Exception);

            // MainWindow is sometimes null because of reasons
            if (this.Application.MainWindow != null)
            {
                this.Application.MainWindow.Hide();
            }

            var windowManager = Locator.Current.GetService<IWindowManager>();
            windowManager.ShowDialog(new CrashViewModel(e.Exception));

            e.Handled = true;

            Application.Current.Shutdown();
        }

        private void ConfigureLogging()
        {
            var logConfig = new LoggingConfiguration();

            var target = new FileTarget
            {
                FileName = AppInfo.LogFilePath,
                Layout = @"${longdate}|${level}|${message} ${exception:format=ToString,StackTrace}",
                ArchiveAboveSize = 1024 * 1024 * 2, // 2 MB
                ArchiveNumbering = ArchiveNumberingMode.Sequence
            };

            logConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, target));
            NLog.LogManager.Configuration = logConfig;

            Locator.CurrentMutable.RegisterConstant(new NLogLogger(NLog.LogManager.GetCurrentClassLogger()), typeof(ILogger));
        }

        private void SetupAnalyticsClient()
        {
            AnalyticsClient.Instance.Initialize(this.coreSettings);
        }

        private void SetupLager()
        {
            this.Log().Info("Initializing Lager settings storages...");

            this.coreSettings.InitializeAsync().Wait();

            // If we don't have a path or it doesn't exist anymore, restore it.
            if (coreSettings.YoutubeDownloadPath == String.Empty || !Directory.Exists(coreSettings.YoutubeDownloadPath))
            {
                coreSettings.YoutubeDownloadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            }

            this.viewSettings.InitializeAsync().Wait();

            this.Log().Info("Settings storages initialized.");
        }

        private void SetupMobileApi()
        {
            var library = Locator.Current.GetService<Library>();

            this.Log().Info("Remote control is {0}", this.coreSettings.EnableRemoteControl ? "enabled" : "disabled");
            this.Log().Info("Port is set to {0}", this.coreSettings.Port);

            IObservable<MobileApi> apiChanged = this.coreSettings.WhenAnyValue(x => x.Port).DistinctUntilChanged()
                .CombineLatest(this.coreSettings.WhenAnyValue(x => x.EnableRemoteControl), Tuple.Create)
                .Do(_ =>
                {
                    if (this.mobileApi != null)
                    {
                        this.mobileApi.Dispose();
                        this.mobileApi = null;
                    }
                })
                .Where(x => x.Item2)
                .Select(x => x.Item1)
                .Select(x => new MobileApi(x, library)).Publish(null).RefCount().Where(x => x != null);

            apiChanged.Subscribe(x =>
            {
                this.mobileApi = x;
                x.SendBroadcastAsync();
                x.StartClientDiscovery();
            });

            IConnectableObservable<IReadOnlyList<MobileClient>> connectedClients = apiChanged.Select(x => x.ConnectedClients).Switch().Publish(new List<MobileClient>());
            connectedClients.Connect();

            IConnectableObservable<bool> isPortOccupied = apiChanged.Select(x => x.IsPortOccupied).Switch().Publish(false);
            isPortOccupied.Connect();

            var apiStats = new MobileApiInfo(connectedClients, isPortOccupied);

            Locator.CurrentMutable.RegisterConstant(apiStats, typeof(MobileApiInfo));
        }
    }
}