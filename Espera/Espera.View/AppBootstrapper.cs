using Akavache;
using Caliburn.Micro;
using Espera.Core.Analytics;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.Services;
using Espera.View.ViewModels;
using Ninject;
using NLog.Config;
using NLog.Targets;
using ReactiveUI;
using ReactiveUI.NLog;
using Shimmer.Client;
using Shimmer.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Espera.View
{
    internal class AppBootstrapper : Bootstrapper<ShellViewModel>, IEnableLogger
    {
        private static readonly string DirectoryPath;
        private static readonly string LibraryFilePath;
        private static readonly string LogFilePath;
        private static readonly string Version;
        private readonly WindowManager windowManager;
        private IKernel kernel;
        private MobileApi mobileApi;

        static AppBootstrapper()
        {
            DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Espera\");
            LibraryFilePath = Path.Combine(DirectoryPath, "Library.json");
            LogFilePath = Path.Combine(DirectoryPath, "Log.txt");
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            BlobCache.ApplicationName = "Espera";
        }

        public AppBootstrapper()
        {
            this.windowManager = new WindowManager();
        }

        protected override void Configure()
        {
            this.kernel = new StandardKernel();
            this.kernel.Settings.AllowNullInjection = true;
            this.kernel.Bind<ILibraryReader>().To<LibraryFileReader>().WithConstructorArgument("sourcePath", LibraryFilePath);
            this.kernel.Bind<ILibraryWriter>().To<LibraryFileWriter>().WithConstructorArgument("targetPath", LibraryFilePath);
            this.kernel.Bind<ViewSettings>().To<ViewSettings>().InSingletonScope();
            this.kernel.Bind<CoreSettings>().To<CoreSettings>().InSingletonScope()
                .OnActivation(x =>
                {
                    // If we don't have a path or it doesn't exist anymore, rest it.
                    if (x.YoutubeDownloadPath == String.Empty || !Directory.Exists(x.YoutubeDownloadPath))
                    {
                        x.YoutubeDownloadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                    }
                });
            this.kernel.Bind<IFileSystem>().To<FileSystem>();
            this.kernel.Bind<Library>().To<Library>().InSingletonScope();
            this.kernel.Bind<IWindowManager>().To<WindowManager>();

            this.ConfigureLogging();
        }

        protected override IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return this.kernel.GetAll(serviceType);
        }

        protected override object GetInstance(Type serviceType, string key)
        {
            return this.kernel.Get(serviceType, key);
        }

        protected override void OnExit(object sender, EventArgs e)
        {
            this.kernel.Dispose();

            BlobCache.Shutdown().Wait();

            NLog.LogManager.Shutdown();

            if (this.mobileApi != null)
            {
                this.mobileApi.Dispose();
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
            this.Log().Info("Current culture: " + CultureInfo.CurrentCulture.Name);

            Directory.CreateDirectory(DirectoryPath);

            this.SetupAnalyticsClient();

            this.SetupLager();

            this.SetupMobileApi();

            this.UpdateSilentlyAsync();

            base.OnStartup(sender, e);
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

            RxApp.MutableResolver.RegisterConstant(new NLogLogger(NLog.LogManager.GetCurrentClassLogger()), typeof(ILogger));
        }

        private async Task SetupAnalyticsClient()
        {
            var coreSettings = this.kernel.Get<CoreSettings>();
            await AnalyticsClient.Instance.InitializeAsync(coreSettings);
        }

        private void SetupLager()
        {
            this.Log().Info("Initializing Lager settings storages...");

            this.kernel.Get<CoreSettings>().InitializeAsync().Wait();
            this.kernel.Get<ViewSettings>().InitializeAsync().Wait();

            this.Log().Info("Settings storages initialized.");
        }

        private void SetupMobileApi()
        {
            var coreSettings = this.kernel.Get<CoreSettings>();
            var library = this.kernel.Get<Library>();

            this.Log().Info("Remote control is {0}", coreSettings.EnableRemoteControl ? "enabled" : "disabled");
            this.Log().Info("Port is set to {0}", coreSettings.Port);

            IObservable<MobileApi> apiChanged = coreSettings.WhenAnyValue(x => x.Port).DistinctUntilChanged()
                .CombineLatest(coreSettings.WhenAnyValue(x => x.EnableRemoteControl), Tuple.Create)
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

            IConnectableObservable<int> connectedClients = apiChanged.Select(x => x.ConnectedClients).Switch().Publish(0);
            connectedClients.Connect();

            var apiStats = new MobileApiInfo(connectedClients);

            this.kernel.Bind<MobileApiInfo>().ToConstant(apiStats);

            coreSettings.WhenAnyValue(x => x.EnableRemoteControl)
                .Where(x => !x && this.mobileApi != null)
                .Subscribe(x => this.mobileApi.Dispose());
        }

        private async Task UpdateSilentlyAsync()
        {
            var settings = this.kernel.Get<ViewSettings>();
#if DEBUG
            string updateUrl = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..", "Releases");
            updateUrl = Path.GetFullPath(updateUrl);
#else
            string updateUrl = null;

            switch (settings.UpdateChannel)
            {
                case UpdateChannel.Dev:
                    updateUrl = "http://espera.s3.amazonaws.com/Releases/Dev";
                    break;

                case UpdateChannel.Stable:
                    updateUrl = "http://espera.s3.amazonaws.com/Releases/Stable";
                    break;
            }
#endif

            using (var updateManager = new UpdateManager(updateUrl, "Espera", FrameworkVersion.Net45))
            {
                this.Log().Info("Looking for application updates at {0}", updateUrl);

                UpdateInfo updateInfo = await updateManager.CheckForUpdate()
                    .LoggedCatch(this, Observable.Return<UpdateInfo>(null), "Error while checking for updates: ");

                if (updateInfo == null)
                    return;

                List<ReleaseEntry> releases = updateInfo.ReleasesToApply.ToList();

                if (releases.Any())
                {
                    this.Log().Info("Found {0} updates.", releases.Count);
                    this.Log().Info("Downloading updates...");

                    try
                    {
                        await updateManager.DownloadReleases(releases);
                    }

                    catch (Exception ex)
                    {
                        this.Log().ErrorException("Failed downloading updates.", ex);
                        return;
                    }

                    this.Log().Info("Updates downloaded.");
                    this.Log().Info("Updating from v{0} to v{1}", updateInfo.CurrentlyInstalledVersion.Version, releases.Last().Version);
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

                    settings.IsUpdated = true;
                }

                else
                {
                    this.Log().Info("No updates found.");
                }
            }
        }
    }
}