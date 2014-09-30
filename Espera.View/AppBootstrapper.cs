using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Akavache;
using Caliburn.Micro;
using Espera.Core;
using Espera.Core.Analytics;
using Espera.Core.Management;
using Espera.Core.Mobile;
using Espera.Core.Settings;
using Espera.View.ViewModels;
using NLog.Config;
using NLog.Targets;
using ReactiveUI;
using Splat;
using Squirrel;

namespace Espera.View
{
    internal class AppBootstrapper : BootstrapperBase, IEnableLogger
    {
        public static readonly string DirectoryPath;
        public static readonly string LibraryFilePath;
        public static readonly string LogFilePath;
        public static readonly string Version;
        private readonly WindowManager windowManager;
        private CoreSettings coreSettings;
        private MobileApi mobileApi;
        private IDisposable updateSubscription;

        private ViewSettings viewSettings;

        static AppBootstrapper()
        {
            string overrideBasePath = null;
            string appName = "Espera";

#if DEBUG
            // Set and uncomment this if you want to change the app data folder for debugging

            // overrideBasePath = "D://AppData";

            appName = "EsperaDebug";
#endif

            DirectoryPath = Path.Combine(overrideBasePath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
            LibraryFilePath = Path.Combine(DirectoryPath, "Library.json");
            LogFilePath = Path.Combine(DirectoryPath, "Log.txt");
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            BlobCache.ApplicationName = appName;

#if DEBUG
            if (overrideBasePath != null)
            {
                BlobCache.LocalMachine = new DebugBlobCache(Path.Combine(DirectoryPath, "BlobCache"));
            }
#endif
        }

        public AppBootstrapper()
        {
            this.windowManager = new WindowManager();
            this.Initialize();
        }

        protected override void Configure()
        {
            this.viewSettings = new ViewSettings();
            Locator.CurrentMutable.RegisterConstant(this.viewSettings, typeof(ViewSettings));

            this.coreSettings = new CoreSettings();

            var library = new Library(new LibraryFileReader(LibraryFilePath),
                new LibraryFileWriter(LibraryFilePath), this.coreSettings, new FileSystem());
            Locator.CurrentMutable.RegisterConstant(library, typeof(Library));

            Locator.CurrentMutable.RegisterConstant(this.windowManager, typeof(IWindowManager));

            Locator.CurrentMutable.Register(() =>
                new ShellViewModel(library, this.viewSettings, this.coreSettings, this.windowManager, Locator.Current.GetService<MobileApiInfo>()),
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
            Locator.Current.GetService<Library>().Dispose();

            BlobCache.Shutdown().Wait();

            NLog.LogManager.Shutdown();

            if (this.mobileApi != null)
            {
                this.mobileApi.Dispose();
            }

            if (this.updateSubscription != null)
            {
                this.updateSubscription.Dispose();
            }
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            this.Log().Info("Espera is starting...");
            this.Log().Info("******************************");
            this.Log().Info("**                          **");
            this.Log().Info("**          Espera          **");
            this.Log().Info("**                          **");
            this.Log().Info("******************************");
            this.Log().Info("Application version: " + Version);
            this.Log().Info("OS Version: " + Environment.OSVersion.VersionString);
            this.Log().Info("Current culture: " + CultureInfo.InstalledUICulture.Name);

            Directory.CreateDirectory(DirectoryPath);

            this.SetupLager();

            this.SetupAnalyticsClient();

            this.SetupMobileApi();

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                this.updateSubscription = Observable.Interval(TimeSpan.FromHours(2), RxApp.TaskpoolScheduler)
                    .StartWith(0) // Trigger an initial update check
                    .SelectMany(x => this.UpdateSilentlyAsync().ToObservable())
                    .Subscribe();
            }

            else
            {
                this.updateSubscription = Disposable.Empty;
            }

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

            this.windowManager.ShowDialog(new CrashViewModel(e.Exception));

            e.Handled = true;

            Application.Current.Shutdown();
        }

        private void ConfigureLogging()
        {
            var logConfig = new LoggingConfiguration();

            var target = new FileTarget
            {
                FileName = LogFilePath,
                Layout = @"${longdate}|${logger}|${level}|${message} ${exception:format=ToString,StackTrace}",
                ArchiveAboveSize = 1024 * 1024 * 2, // 2 MB
                ArchiveNumbering = ArchiveNumberingMode.Sequence
            };

            logConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, target));
            NLog.LogManager.Configuration = logConfig;

            Locator.CurrentMutable.RegisterConstant(new NLogLogger(NLog.LogManager.GetCurrentClassLogger()), typeof(ILogger));
        }

        private async Task SetupAnalyticsClient()
        {
            await AnalyticsClient.Instance.InitializeAsync(this.coreSettings);
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

#if DEBUG
            coreSettings.EnableAutomaticReports = false;
#endif

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
                .Where(x => x.Item2)
                .Select(x => x.Item1)
                .Do(_ =>
                {
                    if (this.mobileApi != null)
                    {
                        this.mobileApi.Dispose();
                    }
                })
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

            this.coreSettings.WhenAnyValue(x => x.EnableRemoteControl)
                .Where(x => !x && this.mobileApi != null)
                .Subscribe(x => this.mobileApi.Dispose());
        }

        private async Task UpdateSilentlyAsync()
        {
            this.Log().Info("Looking for application updates");

            using (var updateManager = new UpdateManager("http://getespera.com/releases/squirrel/", "Espera", FrameworkVersion.Net45))
            {
                UpdateInfo updateInfo;

                try
                {
                    updateInfo = await updateManager.CheckForUpdate();
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
                        .SelectMany(x => BlobCache.LocalMachine.InsertObject(BlobCacheKeys.Changelog, x))
                        .LoggedCatch(this, null, "Could not to fetch changelog")
                        .ToTask();

                    this.Log().Info("Applying updates...");

                    try
                    {
                        await updateManager.ApplyReleases(updateInfo);
                    }

                    catch (Exception ex)
                    {
                        this.Log().Fatal("Failed to apply updates.", ex);
                        AnalyticsClient.Instance.RecordErrorAsync(ex);
                        return;
                    }

                    this.Log().Info("Updates applied.");

                    await changelogFetchTask;

                    this.viewSettings.IsUpdated = true;
                }
            }
        }
    }

#if DEBUG

    internal class DebugBlobCache : Akavache.Deprecated.PersistentBlobCache
    {
        public DebugBlobCache(string path)
            : base(path)
        { }
    }

#endif
}