using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Espera.Core;
using Espera.Core.Analytics;
using Espera.Core.Management;
using Espera.Core.Mobile;
using Espera.Core.Settings;
using Espera.View.CacheMigration;
using Espera.View.ViewModels;
using LogLevel = NLog.LogLevel;
using LogManager = NLog.LogManager;

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
                    v => mgr.CreateShortcutForThisExe(),
                    v =>
                    {
                        mgr.CreateShortcutForThisExe();
                        // Update the shortcut for the portable version
                        mgr.CreateShortcutsForExecutable("Espera.exe", ShortcutLocation.AppRoot, false);
                    },
                    onAppUninstall: v => mgr.RemoveShortcutForThisExe());
            }

            Initialize();
        }

        protected override void Configure()
        {
            viewSettings = new ViewSettings();
            Locator.CurrentMutable.RegisterConstant(viewSettings, typeof(ViewSettings));

            coreSettings = new CoreSettings();

            Locator.CurrentMutable.RegisterLazySingleton(() =>
                new Library(new LibraryFileReader(AppInfo.LibraryFilePath),
                    new LibraryFileWriter(AppInfo.LibraryFilePath), coreSettings, new FileSystem()), typeof(Library));

            Locator.CurrentMutable.RegisterLazySingleton(() => new WindowManager(), typeof(IWindowManager));

            Locator.CurrentMutable.RegisterLazySingleton(
                () => new SQLitePersistentBlobCache(Path.Combine(AppInfo.BlobCachePath, "api-requests.cache.db")),
                typeof(IBlobCache), BlobCacheKeys.RequestCacheContract);

            Locator.CurrentMutable.RegisterLazySingleton(() =>
                    new ShellViewModel(Locator.Current.GetService<Library>(),
                        viewSettings, coreSettings,
                        Locator.Current.GetService<IWindowManager>(),
                        Locator.Current.GetService<MobileApiInfo>()),
                typeof(ShellViewModel));

            ConfigureLogging();
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
            LogManager.Shutdown();

            if (mobileApi != null)
            {
                this.Log().Info("Shutting down mobile API");
                mobileApi.Dispose();
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

            SetupLager();

            SetupAnalyticsClient();

            SetupMobileApi();

            DisplayRootViewFor<ShellViewModel>();
        }

        protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
                return;

            this.Log().FatalException("An unhandled exception occurred, opening the crash report", e.Exception);

            // MainWindow is sometimes null because of reasons
            if (Application.MainWindow != null) Application.MainWindow.Hide();

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

            logConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, target));
            LogManager.Configuration = logConfig;

            Locator.CurrentMutable.RegisterConstant(new NLogLogger(LogManager.GetCurrentClassLogger()),
                typeof(ILogger));
        }

        private void SetupAnalyticsClient()
        {
            AnalyticsClient.Instance.Initialize(coreSettings);
        }

        private void SetupLager()
        {
            this.Log().Info("Initializing Lager settings storages...");

            coreSettings.InitializeAsync().Wait();

            // If we don't have a path or it doesn't exist anymore, restore it.
            if (coreSettings.YoutubeDownloadPath == string.Empty || !Directory.Exists(coreSettings.YoutubeDownloadPath))
                coreSettings.YoutubeDownloadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            viewSettings.InitializeAsync().Wait();

            this.Log().Info("Settings storages initialized.");
        }

        private void SetupMobileApi()
        {
            var library = Locator.Current.GetService<Library>();

            this.Log().Info("Remote control is {0}", coreSettings.EnableRemoteControl ? "enabled" : "disabled");
            this.Log().Info("Port is set to {0}", coreSettings.Port);

            var apiChanged = coreSettings.WhenAnyValue(x => x.Port).DistinctUntilChanged()
                .CombineLatest(coreSettings.WhenAnyValue(x => x.EnableRemoteControl), Tuple.Create)
                .Do(_ =>
                {
                    if (mobileApi != null)
                    {
                        mobileApi.Dispose();
                        mobileApi = null;
                    }
                })
                .Where(x => x.Item2)
                .Select(x => x.Item1)
                .Select(x => new MobileApi(x, library)).Publish(null).RefCount().Where(x => x != null);

            apiChanged.Subscribe(x =>
            {
                mobileApi = x;
                x.SendBroadcastAsync();
                x.StartClientDiscovery();
            });

            var connectedClients =
                apiChanged.Select(x => x.ConnectedClients).Switch().Publish(new List<MobileClient>());
            connectedClients.Connect();

            var isPortOccupied = apiChanged.Select(x => x.IsPortOccupied).Switch().Publish(false);
            isPortOccupied.Connect();

            var apiStats = new MobileApiInfo(connectedClients, isPortOccupied);

            Locator.CurrentMutable.RegisterConstant(apiStats, typeof(MobileApiInfo));
        }
    }
}