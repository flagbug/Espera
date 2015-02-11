using Caliburn.Micro;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Mobile;
using Espera.Core.Settings;
using Espera.Network;
using Rareform.Reflection;
using Rareform.Validation;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;

namespace Espera.View.ViewModels
{
    public class SettingsViewModel : ReactiveObject
    {
        private readonly Guid accessToken;
        private readonly ObservableAsPropertyHelper<bool> canCreateAdmin;
        private readonly ObservableAsPropertyHelper<bool> canLogin;
        private readonly CoreSettings coreSettings;

        private readonly Dictionary<DefaultPlaybackAction, string> defaultPlaybackActionMap = new Dictionary<DefaultPlaybackAction, string>
        {
            {DefaultPlaybackAction.AddToPlaylist, "Add To Playlist"},
            {DefaultPlaybackAction.PlayNow, "Play Now"}
        };

        private readonly ObservableAsPropertyHelper<DefaultPlaybackEngine> defaultPlaybackEngine;

        private readonly Dictionary<DefaultPlaybackEngine, string> defaultPlaybackEngineMap = new Dictionary<DefaultPlaybackEngine, string>
        {
            {DefaultPlaybackEngine.NAudio, "NAudio"},
            {DefaultPlaybackEngine.Wpf, "Windows Media Player"}
        };

        private readonly ObservableAsPropertyHelper<bool> enableChangelog;
        private readonly ObservableAsPropertyHelper<bool> isPortOccupied;
        private readonly ObservableAsPropertyHelper<bool> isRemoteAccessReallyLocked;
        private readonly Library library;
        private readonly ObservableAsPropertyHelper<string> librarySource;
        private readonly ObservableAsPropertyHelper<bool> showRemoteControlPasswordError;
        private readonly ViewSettings viewSettings;
        private readonly IWindowManager windowManager;
        private string creationPassword;
        private bool isAdminCreated;
        private bool isWrongPassword;
        private string loginPassword;
        private int port;
        private string remoteControlPassword;
        private bool showLogin;
        private bool showSettings;

        public SettingsViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, IWindowManager windowManager, Guid accessToken, MobileApiInfo mobileApiInfo)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            if (coreSettings == null)
                Throw.ArgumentNullException(() => coreSettings);

            if (mobileApiInfo == null)
                throw new ArgumentNullException("mobileApiInfo");

            this.library = library;
            this.viewSettings = viewSettings;
            this.coreSettings = coreSettings;
            this.windowManager = windowManager;
            this.accessToken = accessToken;

            this.canCreateAdmin = this
                .WhenAnyValue(x => x.CreationPassword, x => !string.IsNullOrWhiteSpace(x) && !this.isAdminCreated)
                .ToProperty(this, x => x.CanCreateAdmin);

            this.CreateAdminCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanCreateAdmin),
                ImmediateScheduler.Instance); // Immediate execution, because we set the password to an empty string afterwards
            this.CreateAdminCommand.Subscribe(p =>
            {
                this.library.LocalAccessControl.SetLocalPassword(this.accessToken, this.CreationPassword);
                this.isAdminCreated = true;
            });

            this.ChangeToPartyCommand = ReactiveCommand.Create(this.CreateAdminCommand.Select(x => true).StartWith(false));
            this.ChangeToPartyCommand.Subscribe(p =>
            {
                this.library.LocalAccessControl.DowngradeLocalAccess(this.accessToken);
                this.ShowSettings = false;
            });

            this.canLogin = this.WhenAnyValue(x => x.LoginPassword, x => !string.IsNullOrWhiteSpace(x))
                .ToProperty(this, x => x.CanLogin);

            this.LoginCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanLogin),
                ImmediateScheduler.Instance); // Immediate execution, because we set the password to an empty string afterwards
            this.LoginCommand.Subscribe(p =>
            {
                try
                {
                    this.library.LocalAccessControl.UpgradeLocalAccess(this.accessToken, this.LoginPassword);
                    this.IsWrongPassword = false;

                    this.ShowLogin = false;
                    this.ShowSettings = true;
                }

                catch (WrongPasswordException)
                {
                    this.IsWrongPassword = true;
                }
            });

            this.OpenLinkCommand = ReactiveCommand.Create();
            this.OpenLinkCommand.Cast<string>().Subscribe(x =>
            {
                try
                {
                    Process.Start(x);
                }

                catch (Win32Exception ex)
                {
                    this.Log().ErrorException(String.Format("Could not open link {0}", x), ex);
                }
            });

            this.ReportBugCommand = ReactiveCommand.Create();
            this.ReportBugCommand.Subscribe(p => this.windowManager.ShowWindow(new BugReportViewModel()));

            this.ChangeAccentColorCommand = ReactiveCommand.Create();
            this.ChangeAccentColorCommand.Subscribe(x => this.viewSettings.AccentColor = (string)x);

            this.ChangeAppThemeCommand = ReactiveCommand.Create();
            this.ChangeAppThemeCommand.Subscribe(x => this.viewSettings.AppTheme = (string)x);

            this.UpdateLibraryCommand = ReactiveCommand.Create(this.library.WhenAnyValue(x => x.IsUpdating, x => !x)
                .ObserveOn(RxApp.MainThreadScheduler)
                .CombineLatest(this.library.WhenAnyValue(x => x.SongSourcePath).Select(x => !String.IsNullOrEmpty(x)), (x1, x2) => x1 && x2));
            this.UpdateLibraryCommand.Subscribe(_ => this.library.UpdateNow());

            this.librarySource = this.library.WhenAnyValue(x => x.SongSourcePath)
                .ToProperty(this, x => x.LibrarySource);

            this.port = this.coreSettings.Port;
            this.ChangePortCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.Port, NetworkHelpers.IsPortValid));
            this.ChangePortCommand.Subscribe(_ => this.coreSettings.Port = this.Port);

            this.remoteControlPassword = this.coreSettings.RemoteControlPassword;
            this.ChangeRemoteControlPasswordCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.RemoteControlPassword)
                .Select(x => !String.IsNullOrWhiteSpace(x)));
            this.ChangeRemoteControlPasswordCommand.Subscribe(x =>
                this.library.RemoteAccessControl.SetRemotePassword(this.accessToken, this.RemoteControlPassword));
            this.showRemoteControlPasswordError = this.WhenAnyValue(x => x.RemoteControlPassword, x => x.LockRemoteControl,
                    (password, lockRemoteControl) => String.IsNullOrWhiteSpace(password) && lockRemoteControl)
                .ToProperty(this, x => x.ShowRemoteControlPasswordError);

            this.isRemoteAccessReallyLocked = this.library.RemoteAccessControl.WhenAnyValue(x => x.IsRemoteAccessReallyLocked)
                .ToProperty(this, x => x.IsRemoteAccessReallyLocked);

            this.isPortOccupied = mobileApiInfo.IsPortOccupied.ToProperty(this, x => x.IsPortOccupied);

            this.enableChangelog = this.viewSettings.WhenAnyValue(x => x.EnableChangelog)
                .ToProperty(this, x => x.EnableChangelog);

            this.defaultPlaybackEngine = this.coreSettings.WhenAnyValue(x => x.DefaultPlaybackEngine)
                .ToProperty(this, x => x.DefaultPlaybackEngine);
        }

        public static IEnumerable<YoutubeStreamingQuality> YoutubeStreamingQualities
        {
            get
            {
                return Enum.GetValues(typeof(YoutubeStreamingQuality))
                    .Cast<YoutubeStreamingQuality>()
                    .Reverse();
            }
        }

        public bool CanCreateAdmin
        {
            get { return this.canCreateAdmin.Value; }
        }

        public bool CanLogin
        {
            get { return this.canLogin.Value; }
        }

        public ReactiveCommand<object> ChangeAccentColorCommand { get; private set; }

        public ReactiveCommand<object> ChangeAppThemeCommand { get; private set; }

        public ReactiveCommand<object> ChangePortCommand { get; private set; }

        public ReactiveCommand<object> ChangeRemoteControlPasswordCommand { get; private set; }

        public ReactiveCommand<object> ChangeToPartyCommand { get; private set; }

        public ReactiveCommand<object> CreateAdminCommand { get; private set; }

        public string CreationPassword
        {
            private get { return this.creationPassword; }
            set { this.RaiseAndSetIfChanged(ref this.creationPassword, value); }
        }

        public string DefaultPlaybackActionString
        {
            get { return this.defaultPlaybackActionMap[this.coreSettings.DefaultPlaybackAction]; }
            set
            {
                this.coreSettings.DefaultPlaybackAction = this.defaultPlaybackActionMap.Single(x => x.Value == value).Key;
                this.RaisePropertyChanged();
            }
        }

        public IEnumerable<string> DefaultPlaybackActionStrings
        {
            get
            {
                return Enum.GetValues(typeof(DefaultPlaybackAction))
                    .Cast<DefaultPlaybackAction>()
                    .Select(x => this.defaultPlaybackActionMap[x]);
            }
        }

        public DefaultPlaybackEngine DefaultPlaybackEngine
        {
            get { return this.defaultPlaybackEngine.Value; }
        }

        public string DefaultPlaybackEngineString
        {
            get { return this.defaultPlaybackEngineMap[this.coreSettings.DefaultPlaybackEngine]; }
            set
            {
                this.coreSettings.DefaultPlaybackEngine = this.defaultPlaybackEngineMap.Single(x => x.Value == value).Key;
                this.RaisePropertyChanged();
            }
        }

        public IEnumerable<string> DefaultPlaybackEngineStrings
        {
            get
            {
                return Enum.GetValues(typeof(DefaultPlaybackEngine))
                    .Cast<DefaultPlaybackEngine>()
                    .Select(x => this.defaultPlaybackEngineMap[x]);
            }
        }

        public string DonationPage
        {
            get { return "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=K5AWR8EDG9QJY"; }
        }

        public bool EnableAutomaticLibraryUpdates
        {
            get { return this.coreSettings.EnableAutomaticLibraryUpdates; }
            set
            {
                this.coreSettings.EnableAutomaticLibraryUpdates = value;
                this.RaisePropertyChanged();
            }
        }

        public bool EnableAutomaticReports
        {
            get { return this.coreSettings.EnableAutomaticReports; }
            set { this.coreSettings.EnableAutomaticReports = value; }
        }

        public bool EnableChangelog
        {
            get { return this.enableChangelog.Value; }
            set { this.viewSettings.EnableChangelog = value; }
        }

        public bool EnableGuestSystem
        {
            get { return this.coreSettings.EnableGuestSystem; }
            set { this.coreSettings.EnableGuestSystem = value; }
        }

        public bool EnableRemoteControl
        {
            get { return this.coreSettings.EnableRemoteControl; }
            set
            {
                this.coreSettings.EnableRemoteControl = value;
                this.RaisePropertyChanged();
            }
        }

        public string Error { get; private set; }

        public bool GoFullScreenOnLock
        {
            get { return this.viewSettings.GoFullScreenOnLock; }
            set { this.viewSettings.GoFullScreenOnLock = value; }
        }

        public string Homepage
        {
            get { return "http://getespera.com"; }
        }

        public bool IsPortOccupied
        {
            get { return this.isPortOccupied.Value; }
        }

        public bool IsRemoteAccessReallyLocked
        {
            get { return this.isRemoteAccessReallyLocked.Value; }
        }

        public bool IsWrongPassword
        {
            get { return this.isWrongPassword; }
            set { this.RaiseAndSetIfChanged(ref this.isWrongPassword, value); }
        }

        public string LibrarySource
        {
            get { return this.librarySource.Value; }
        }

        public bool LockPlaylist
        {
            get { return this.coreSettings.LockPlaylist; }
            set { this.coreSettings.LockPlaylist = value; }
        }

        public bool LockPlayPause
        {
            get { return this.coreSettings.LockPlayPause; }
            set { this.coreSettings.LockPlayPause = value; }
        }

        public bool LockRemoteControl
        {
            get { return this.coreSettings.LockRemoteControl; }
            set
            {
                this.coreSettings.LockRemoteControl = value;
                this.RaisePropertyChanged();
            }
        }

        public bool LockTime
        {
            get { return this.coreSettings.LockTime; }
            set { this.coreSettings.LockTime = value; }
        }

        public bool LockVolume
        {
            get { return this.coreSettings.LockVolume; }
            set { this.coreSettings.LockVolume = value; }
        }

        public bool LockWindow
        {
            get { return this.viewSettings.LockWindow; }
            set
            {
                if (this.LockWindow != value)
                {
                    this.viewSettings.LockWindow = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public string LogFilePath
        {
            get { return AppInfo.LogFilePath; }
        }

        public ReactiveCommand<object> LoginCommand { get; private set; }

        public string LoginPassword
        {
            private get { return this.loginPassword; }
            set { this.RaiseAndSetIfChanged(ref this.loginPassword, value); }
        }

        public ReactiveCommand<object> OpenLinkCommand { get; private set; }

        public string PlayStoreLink
        {
            get { return "http://play.google.com/store/apps/details?id=com.flagbug.esperamobile"; }
        }

        public int Port
        {
            get { return this.port; }
            set { this.RaiseAndSetIfChanged(ref port, value); }
        }

        public string ReleaseNotes
        {
            get { return "http://getespera.com/release-notes"; }
        }

        public string RemoteControlPassword
        {
            get { return this.remoteControlPassword; }
            set { this.RaiseAndSetIfChanged(ref this.remoteControlPassword, value); }
        }

        public ReactiveCommand<object> ReportBugCommand { get; private set; }

        public double Scaling
        {
            get { return this.viewSettings.Scaling; }
            set
            {
                this.viewSettings.Scaling = value;
                this.RaisePropertyChanged();
            }
        }

        public bool ShowLogin
        {
            get { return this.showLogin; }
            set { this.RaiseAndSetIfChanged(ref this.showLogin, value); }
        }

        public bool ShowRemoteControlPasswordError
        {
            get { return this.showRemoteControlPasswordError.Value; }
        }

        public bool ShowSettings
        {
            get { return this.showSettings; }
            set { this.RaiseAndSetIfChanged(ref this.showSettings, value); }
        }

        public double SongSourceUpdateInterval
        {
            get { return this.coreSettings.SongSourceUpdateInterval.TotalHours; }
            set
            {
                this.coreSettings.SongSourceUpdateInterval = TimeSpan.FromHours(value);

                this.RaisePropertyChanged();
            }
        }

        public bool StreamHighestYoutubeQuality
        {
            get { return this.coreSettings.StreamHighestYoutubeQuality; }
            set
            {
                this.coreSettings.StreamHighestYoutubeQuality = value;

                this.RaisePropertyChanged();
            }
        }

        public ReactiveCommand<object> UpdateLibraryCommand { get; private set; }

        public string Version
        {
            get
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;

                return String.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Revision);
            }
        }

        public string YoutubeDownloadPath
        {
            get { return this.coreSettings.YoutubeDownloadPath; }
            set
            {
                this.coreSettings.YoutubeDownloadPath = value;
                this.RaisePropertyChanged();
            }
        }

        public YoutubeStreamingQuality YoutubeStreamingQuality
        {
            get { return this.coreSettings.YoutubeStreamingQuality; }
            set
            {
                this.coreSettings.YoutubeStreamingQuality = value;
                this.RaisePropertyChanged();
            }
        }

        public void ChangeLibrarySource(string source)
        {
            this.library.ChangeSongSourcePath(source, this.accessToken);
        }

        public void HandleSettings()
        {
            if (this.isAdminCreated && this.library.LocalAccessControl.ObserveAccessPermission(this.accessToken).FirstAsync().Wait() == AccessPermission.Guest)
            {
                this.ShowLogin = true;
            }

            else
            {
                this.ShowSettings = true;
            }
        }
    }
}