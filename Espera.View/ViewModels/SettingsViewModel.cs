using System;
using System.Collections.Generic;
using System.Reflection;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Mobile;
using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    public class SettingsViewModel : ReactiveObject
    {
        private readonly Guid accessToken;
        private readonly ObservableAsPropertyHelper<bool> canCreateAdmin;
        private readonly ObservableAsPropertyHelper<bool> canLogin;
        private readonly CoreSettings coreSettings;

        private readonly Dictionary<DefaultPlaybackAction, string> defaultPlaybackActionMap =
            new Dictionary<DefaultPlaybackAction, string>
            {
                { DefaultPlaybackAction.AddToPlaylist, "Add To Playlist" },
                { DefaultPlaybackAction.PlayNow, "Play Now" }
            };

        private readonly ObservableAsPropertyHelper<DefaultPlaybackEngine> defaultPlaybackEngine;

        private readonly Dictionary<DefaultPlaybackEngine, string> defaultPlaybackEngineMap =
            new Dictionary<DefaultPlaybackEngine, string>
            {
                { DefaultPlaybackEngine.NAudio, "NAudio" },
                { DefaultPlaybackEngine.Wpf, "Windows Media Player" }
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

        public SettingsViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings,
            IWindowManager windowManager, Guid accessToken, MobileApiInfo mobileApiInfo)
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

            canCreateAdmin = this
                .WhenAnyValue<SettingsViewModel, bool, string>(x => x.CreationPassword,
                    x => !string.IsNullOrWhiteSpace(x) && !isAdminCreated)
                .ToProperty(this, x => x.CanCreateAdmin);

            CreateAdminCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanCreateAdmin),
                ImmediateScheduler
                    .Instance); // Immediate execution, because we set the password to an empty string afterwards
            CreateAdminCommand.Subscribe(p =>
            {
                this.library.LocalAccessControl.SetLocalPassword(this.accessToken, CreationPassword);
                isAdminCreated = true;
            });

            ChangeToPartyCommand = ReactiveCommand.Create(CreateAdminCommand.Select(x => true).StartWith(false));
            ChangeToPartyCommand.Subscribe(p =>
            {
                this.library.LocalAccessControl.DowngradeLocalAccess(this.accessToken);
                ShowSettings = false;
            });

            canLogin = this
                .WhenAnyValue<SettingsViewModel, bool, string>(x => x.LoginPassword, x => !string.IsNullOrWhiteSpace(x))
                .ToProperty(this, x => x.CanLogin);

            LoginCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanLogin),
                ImmediateScheduler
                    .Instance); // Immediate execution, because we set the password to an empty string afterwards
            LoginCommand.Subscribe(p =>
            {
                try
                {
                    this.library.LocalAccessControl.UpgradeLocalAccess(this.accessToken, LoginPassword);
                    IsWrongPassword = false;

                    ShowLogin = false;
                    ShowSettings = true;
                }

                catch (WrongPasswordException)
                {
                    IsWrongPassword = true;
                }
            });

            OpenLinkCommand = ReactiveCommand.Create();
            OpenLinkCommand.Cast<string>().Subscribe(x =>
            {
                try
                {
                    Process.Start(x);
                }

                catch (Win32Exception ex)
                {
                    this.Log().ErrorException(string.Format("Could not open link {0}", x), ex);
                }
            });

            ReportBugCommand = ReactiveCommand.Create();
            ReportBugCommand.Subscribe(p => this.windowManager.ShowWindow(new BugReportViewModel()));

            ChangeAccentColorCommand = ReactiveCommand.Create();
            ChangeAccentColorCommand.Subscribe(x => this.viewSettings.AccentColor = (string)x);

            ChangeAppThemeCommand = ReactiveCommand.Create();
            ChangeAppThemeCommand.Subscribe(x => this.viewSettings.AppTheme = (string)x);

            UpdateLibraryCommand = ReactiveCommand.Create(this.library.WhenAnyValue(x => x.IsUpdating, x => !x)
                .ObserveOn(RxApp.MainThreadScheduler)
                .CombineLatest(this.library.WhenAnyValue(x => x.SongSourcePath).Select(x => !string.IsNullOrEmpty(x)),
                    (x1, x2) => x1 && x2));
            UpdateLibraryCommand.Subscribe(_ => this.library.UpdateNow());

            librarySource = this.library.WhenAnyValue(x => x.SongSourcePath)
                .ToProperty(this, x => x.LibrarySource);

            port = this.coreSettings.Port;
            ChangePortCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.Port, NetworkHelpers.IsPortValid));
            ChangePortCommand.Subscribe(_ => this.coreSettings.Port = Port);

            remoteControlPassword = this.coreSettings.RemoteControlPassword;
            ChangeRemoteControlPasswordCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.RemoteControlPassword)
                .Select(x => !string.IsNullOrWhiteSpace(x)));
            ChangeRemoteControlPasswordCommand.Subscribe(x =>
                this.library.RemoteAccessControl.SetRemotePassword(this.accessToken, RemoteControlPassword));
            showRemoteControlPasswordError = this.WhenAnyValue(x => x.RemoteControlPassword, x => x.LockRemoteControl,
                    (password, lockRemoteControl) => string.IsNullOrWhiteSpace(password) && lockRemoteControl)
                .ToProperty(this, x => x.ShowRemoteControlPasswordError);

            isRemoteAccessReallyLocked = this.library.RemoteAccessControl
                .WhenAnyValue(x => x.IsRemoteAccessReallyLocked)
                .ToProperty(this, x => x.IsRemoteAccessReallyLocked);

            isPortOccupied = mobileApiInfo.IsPortOccupied.ToProperty(this, x => x.IsPortOccupied);

            enableChangelog = this.viewSettings.WhenAnyValue(x => x.EnableChangelog)
                .ToProperty(this, x => x.EnableChangelog);

            defaultPlaybackEngine = this.coreSettings.WhenAnyValue(x => x.DefaultPlaybackEngine)
                .ToProperty(this, x => x.DefaultPlaybackEngine);
        }

        public static IEnumerable<YoutubeStreamingQuality> YoutubeStreamingQualities =>
            Enum.GetValues(typeof(YoutubeStreamingQuality))
                .Cast<YoutubeStreamingQuality>()
                .Reverse();

        public bool CanCreateAdmin => canCreateAdmin.Value;

        public bool CanLogin => canLogin.Value;

        public ReactiveCommand<object> ChangeAccentColorCommand { get; }

        public ReactiveCommand<object> ChangeAppThemeCommand { get; }

        public ReactiveCommand<object> ChangePortCommand { get; }

        public ReactiveCommand<object> ChangeRemoteControlPasswordCommand { get; }

        public ReactiveCommand<object> ChangeToPartyCommand { get; }

        public ReactiveCommand<object> CreateAdminCommand { get; }

        public string CreationPassword
        {
            private get { return creationPassword; }
            set { this.RaiseAndSetIfChanged(ref creationPassword, value); }
        }

        public string DefaultPlaybackActionString
        {
            get => defaultPlaybackActionMap[coreSettings.DefaultPlaybackAction];
            set
            {
                coreSettings.DefaultPlaybackAction = defaultPlaybackActionMap.Single(x => x.Value == value).Key;
                this.RaisePropertyChanged();
            }
        }

        public IEnumerable<string> DefaultPlaybackActionStrings
        {
            get
            {
                return Enum.GetValues(typeof(DefaultPlaybackAction))
                    .Cast<DefaultPlaybackAction>()
                    .Select(x => defaultPlaybackActionMap[x]);
            }
        }

        public DefaultPlaybackEngine DefaultPlaybackEngine => defaultPlaybackEngine.Value;

        public string DefaultPlaybackEngineString
        {
            get => defaultPlaybackEngineMap[coreSettings.DefaultPlaybackEngine];
            set
            {
                coreSettings.DefaultPlaybackEngine = defaultPlaybackEngineMap.Single(x => x.Value == value).Key;
                this.RaisePropertyChanged();
            }
        }

        public IEnumerable<string> DefaultPlaybackEngineStrings
        {
            get
            {
                return Enum.GetValues(typeof(DefaultPlaybackEngine))
                    .Cast<DefaultPlaybackEngine>()
                    .Select(x => defaultPlaybackEngineMap[x]);
            }
        }

        public string DonationPage =>
            "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=K5AWR8EDG9QJY";

        public bool EnableAutomaticLibraryUpdates
        {
            get => coreSettings.EnableAutomaticLibraryUpdates;
            set
            {
                coreSettings.EnableAutomaticLibraryUpdates = value;
                this.RaisePropertyChanged();
            }
        }

        public bool EnableAutomaticReports
        {
            get => coreSettings.EnableAutomaticReports;
            set => coreSettings.EnableAutomaticReports = value;
        }

        public bool EnableChangelog
        {
            get => enableChangelog.Value;
            set => viewSettings.EnableChangelog = value;
        }

        public bool EnableGuestSystem
        {
            get => coreSettings.EnableGuestSystem;
            set => coreSettings.EnableGuestSystem = value;
        }

        public bool EnableRemoteControl
        {
            get => coreSettings.EnableRemoteControl;
            set
            {
                coreSettings.EnableRemoteControl = value;
                this.RaisePropertyChanged();
            }
        }

        public string Error { get; private set; }

        public bool GoFullScreenOnLock
        {
            get => viewSettings.GoFullScreenOnLock;
            set => viewSettings.GoFullScreenOnLock = value;
        }

        public string Homepage => "http://getespera.com";

        public bool IsPortOccupied => isPortOccupied.Value;

        public bool IsRemoteAccessReallyLocked => isRemoteAccessReallyLocked.Value;

        public bool IsWrongPassword
        {
            get => isWrongPassword;
            set => this.RaiseAndSetIfChanged(ref isWrongPassword, value);
        }

        public string LibrarySource => librarySource.Value;

        public bool LockPlaylist
        {
            get => coreSettings.LockPlaylist;
            set => coreSettings.LockPlaylist = value;
        }

        public bool LockPlayPause
        {
            get => coreSettings.LockPlayPause;
            set => coreSettings.LockPlayPause = value;
        }

        public bool LockRemoteControl
        {
            get => coreSettings.LockRemoteControl;
            set
            {
                coreSettings.LockRemoteControl = value;
                this.RaisePropertyChanged();
            }
        }

        public bool LockTime
        {
            get => coreSettings.LockTime;
            set => coreSettings.LockTime = value;
        }

        public bool LockVolume
        {
            get => coreSettings.LockVolume;
            set => coreSettings.LockVolume = value;
        }

        public bool LockWindow
        {
            get => viewSettings.LockWindow;
            set
            {
                if (LockWindow != value)
                {
                    viewSettings.LockWindow = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public string LogFilePath => AppInfo.LogFilePath;

        public ReactiveCommand<object> LoginCommand { get; }

        public string LoginPassword
        {
            private get { return loginPassword; }
            set { this.RaiseAndSetIfChanged(ref loginPassword, value); }
        }

        public ReactiveCommand<object> OpenLinkCommand { get; }

        public string PlayStoreLink => "http://play.google.com/store/apps/details?id=com.flagbug.esperamobile";

        public int Port
        {
            get => port;
            set => this.RaiseAndSetIfChanged(ref port, value);
        }

        public string ReleaseNotes => "http://getespera.com/release-notes";

        public string RemoteControlPassword
        {
            get => remoteControlPassword;
            set => this.RaiseAndSetIfChanged(ref remoteControlPassword, value);
        }

        public ReactiveCommand<object> ReportBugCommand { get; }

        public double Scaling
        {
            get => viewSettings.Scaling;
            set
            {
                viewSettings.Scaling = value;
                this.RaisePropertyChanged();
            }
        }

        public bool ShowLogin
        {
            get => showLogin;
            set => this.RaiseAndSetIfChanged(ref showLogin, value);
        }

        public bool ShowRemoteControlPasswordError => showRemoteControlPasswordError.Value;

        public bool ShowSettings
        {
            get => showSettings;
            set => this.RaiseAndSetIfChanged(ref showSettings, value);
        }

        public double SongSourceUpdateInterval
        {
            get => coreSettings.SongSourceUpdateInterval.TotalHours;
            set
            {
                coreSettings.SongSourceUpdateInterval = TimeSpan.FromHours(value);

                this.RaisePropertyChanged();
            }
        }

        public bool StreamHighestYoutubeQuality
        {
            get => coreSettings.StreamHighestYoutubeQuality;
            set
            {
                coreSettings.StreamHighestYoutubeQuality = value;

                this.RaisePropertyChanged();
            }
        }

        public ReactiveCommand<object> UpdateLibraryCommand { get; }

        public string Version
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;

                return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Revision);
            }
        }

        public string YoutubeDownloadPath
        {
            get => coreSettings.YoutubeDownloadPath;
            set
            {
                coreSettings.YoutubeDownloadPath = value;
                this.RaisePropertyChanged();
            }
        }

        public YoutubeStreamingQuality YoutubeStreamingQuality
        {
            get => coreSettings.YoutubeStreamingQuality;
            set
            {
                coreSettings.YoutubeStreamingQuality = value;
                this.RaisePropertyChanged();
            }
        }

        public void ChangeLibrarySource(string source)
        {
            library.ChangeSongSourcePath(source, accessToken);
        }

        public void HandleSettings()
        {
            if (isAdminCreated && library.LocalAccessControl.ObserveAccessPermission(accessToken).FirstAsync().Wait() ==
                AccessPermission.Guest)
                ShowLogin = true;

            else
                ShowSettings = true;
        }
    }
}