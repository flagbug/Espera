using Caliburn.Micro;
using Espera.Core.Management;
using Espera.View.Properties;
using Rareform.Validation;
using ReactiveUI;
using ReactiveUI.Xaml;
using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;

namespace Espera.View.ViewModels
{
    internal class SettingsViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool> canCreateAdmin;
        private readonly ObservableAsPropertyHelper<bool> canLogin;
        private readonly Library library;
        private readonly IWindowManager windowManager;
        private string creationPassword;
        private bool isWrongPassword;
        private string loginPassword;
        private bool showLogin;
        private bool showSettings;

        public SettingsViewModel(Library library, IWindowManager windowManager)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.library = library;

            this.windowManager = windowManager;

            this.canCreateAdmin = this
                .WhenAny(x => x.CreationPassword, x => !string.IsNullOrWhiteSpace(x.Value) && !this.IsAdminCreated)
                .ToProperty(this, x => x.CanCreateAdmin);

            this.CreateAdminCommand = new ReactiveCommand(this.canCreateAdmin,
                ImmediateScheduler.Instance); // Immediate execution, because we set the password to an empty string afterwards
            this.CreateAdminCommand.Subscribe(p =>
            {
                this.library.CreateAdmin(this.CreationPassword);

                this.RaisePropertyChanged(x => x.IsAdminCreated);
            });

            this.canLogin = this.WhenAny(x => x.LoginPassword, x => !string.IsNullOrWhiteSpace(x.Value))
                .ToProperty(this, x => x.CanLogin);

            this.LoginCommand = new ReactiveCommand(this.canLogin,
                ImmediateScheduler.Instance); // Immediate execution, because we set the password to an empty string afterwards
            this.LoginCommand.Subscribe(p =>
            {
                try
                {
                    this.library.ChangeToAdmin(this.LoginPassword);
                    this.IsWrongPassword = false;

                    this.ShowLogin = false;
                    this.ShowSettings = false;
                }

                catch (WrongPasswordException)
                {
                    this.IsWrongPassword = true;
                }
            });

            this.OpenLinkCommand = new ReactiveCommand();
            this.OpenLinkCommand.Subscribe(p => Process.Start((string)p));

            this.ReportBugCommand = new ReactiveCommand();
            this.ReportBugCommand.Subscribe(p => this.windowManager.ShowWindow(new BugReportViewModel()));

            this.ChangeAccentColorCommand = new ReactiveCommand();
            this.ChangeAccentColorCommand.Subscribe(p => Settings.Default.AccentColor = (string)p);
        }

        public bool CanCreateAdmin
        {
            get { return this.canCreateAdmin.Value; }
        }

        public bool CanLogin
        {
            get { return this.canLogin.Value; }
        }

        public IReactiveCommand ChangeAccentColorCommand { get; private set; }

        public IReactiveCommand ChangeToPartyCommand { get; private set; }

        public IReactiveCommand CreateAdminCommand { get; private set; }

        public string CreationPassword
        {
            private get { return this.creationPassword; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public string DonationPage
        {
            get { return "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=K5AWR8EDG9QJY"; }
        }

        public bool EnablePlaylistTimeout
        {
            get { return this.library.EnablePlaylistTimeout; }
            set
            {
                if (this.library.EnablePlaylistTimeout != value)
                {
                    this.library.EnablePlaylistTimeout = value;

                    this.RaisePropertyChanged(x => x.EnablePlaylistTimeout);
                }
            }
        }

        public bool GoFullScreenOnLock
        {
            get { return Settings.Default.GoFullScreenOnLock; }
            set { Settings.Default.GoFullScreenOnLock = value; }
        }

        public string Homepage
        {
            get { return "http://espera.flagbug.com"; }
        }

        public bool IsAdminCreated
        {
            get { return this.library.IsAdministratorCreated; }
        }

        public bool IsWrongPassword
        {
            get { return this.isWrongPassword; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool LockLibraryRemoval
        {
            get { return this.library.LockLibraryRemoval; }
            set { this.library.LockLibraryRemoval = value; }
        }

        public bool LockPlaylistRemoval
        {
            get { return this.library.LockPlaylistRemoval; }
            set { this.library.LockPlaylistRemoval = value; }
        }

        public bool LockPlaylistSwitching
        {
            get { return this.library.LockPlaylistSwitching; }
            set { this.library.LockPlaylistSwitching = value; }
        }

        public bool LockPlayPause
        {
            get { return this.library.LockPlayPause; }
            set { this.library.LockPlayPause = value; }
        }

        public bool LockTime
        {
            get { return this.library.LockTime; }
            set { this.library.LockTime = value; }
        }

        public bool LockVolume
        {
            get { return this.library.LockVolume; }
            set { this.library.LockVolume = value; }
        }

        public bool LockWindow
        {
            get { return Settings.Default.LockWindow; }
            set
            {
                if (this.LockWindow != value)
                {
                    Settings.Default.LockWindow = value;
                    this.RaisePropertyChanged(x => x.LockWindow);
                }
            }
        }

        public IReactiveCommand LoginCommand { get; private set; }

        public string LoginPassword
        {
            private get { return this.loginPassword; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public IReactiveCommand OpenLinkCommand { get; private set; }

        public int PlaylistTimeout
        {
            get { return (int)this.library.PlaylistTimeout.TotalSeconds; }
            set { this.library.PlaylistTimeout = TimeSpan.FromSeconds(value); }
        }

        public string ReleaseNotes
        {
            get { return "http://espera.flagbug.com/release-notes"; }
        }

        public IReactiveCommand ReportBugCommand { get; private set; }

        public bool ShowLogin
        {
            get { return this.showLogin; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool ShowSettings
        {
            get { return this.showSettings; }
            private set { this.RaiseAndSetIfChanged(value); }
        }

        public bool StreamYoutube
        {
            get { return this.library.StreamYoutube; }
            set { this.library.StreamYoutube = value; }
        }

        public string Version
        {
            get
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;

                return String.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Revision);
            }
        }

        public void HandleSettings()
        {
            if (this.IsAdminCreated && this.library.AccessMode.First() == AccessMode.Party)
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