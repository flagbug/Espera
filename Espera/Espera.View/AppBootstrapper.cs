using Akavache;
using Caliburn.Micro;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.View.ViewModels;
using Microsoft.WindowsAPICodePack.Shell;
using Ninject;
using NLog.Config;
using NLog.Targets;
using ReactiveUI;
using ReactiveUI.NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
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

        static AppBootstrapper()
        {
            DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Espera\");
            LibraryFilePath = Path.Combine(DirectoryPath, "Library.xml");
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
            this.kernel.Bind<ILibraryReader>().To<LibraryFileReader>().WithConstructorArgument("sourcePath", LibraryFilePath);
            this.kernel.Bind<ILibraryWriter>().To<LibraryFileWriter>().WithConstructorArgument("targetPath", LibraryFilePath);
            this.kernel.Bind<ViewSettings>().To<ViewSettings>().InSingletonScope().WithConstructorArgument("blobCache", BlobCache.LocalMachine);
            this.kernel.Bind<CoreSettings>().To<CoreSettings>().InSingletonScope().WithConstructorArgument("blobCache", BlobCache.LocalMachine)
                .OnActivation(x =>
                {
                    if (x.YoutubeDownloadPath == String.Empty)
                    {
                        x.YoutubeDownloadPath = KnownFolders.Downloads.Path;
                    }
                });
            this.kernel.Bind<IFileSystem>().To<FileSystem>();
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

            this.Log().Info("Initializing Lager settings storages...");

            this.kernel.Get<CoreSettings>().InitializeAsync().Wait();
            this.kernel.Get<ViewSettings>().InitializeAsync().Wait();

            this.Log().Info("Settings storages initialized.");

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
                Layout = @"${longdate}|${logger}|${level}|${message} ${exception:format=ToString,StackTrace}"
            };

            logConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, target));
            NLog.LogManager.Configuration = logConfig;

            RxApp.MutableResolver.RegisterConstant(new NLogLogger(NLog.LogManager.GetCurrentClassLogger()), typeof(ILogger));
        }
    }
}