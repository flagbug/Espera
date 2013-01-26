using Caliburn.Micro;
using Espera.Core.Management;
using Espera.View.Properties;
using Rareform.Patterns.MVVM;
using Rareform.Validation;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class SettingsViewModel : PropertyChangedBase
    {
        private readonly Library library;
        private readonly IWindowManager windowManager;
        private bool isWrongPassword;
        private bool showLogin;
        private bool showSettings;

        public SettingsViewModel(Library library, IWindowManager windowManager)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.library = library;

            this.windowManager = windowManager;
        }

        public ICommand ChangeAccentColorCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => Settings.Default.AccentColor = (string)param
                );
            }
        }

        public ICommand ChangeToPartyCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.library.ChangeToParty();
                        this.ShowSettings = false;
                    },
                    param => this.IsAdminCreated
                );
            }
        }

        public ICommand CreateAdminCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        this.library.CreateAdmin(this.CreationPassword);

                        this.NotifyOfPropertyChange(() => this.IsAdminCreated);
                    },
                    param => !string.IsNullOrWhiteSpace(this.CreationPassword) && !this.IsAdminCreated
                );
            }
        }

        public string CreationPassword { get; set; }

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

                    this.NotifyOfPropertyChange(() => this.EnablePlaylistTimeout);
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
            set
            {
                if (this.IsWrongPassword != value)
                {
                    this.isWrongPassword = value;
                    this.NotifyOfPropertyChange(() => this.IsWrongPassword);
                }
            }
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
                    this.NotifyOfPropertyChange(() => this.LockWindow);
                }
            }
        }

        public ICommand LoginCommand
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        try
                        {
                            this.library.ChangeToAdmin(this.LoginPassword);
                            this.IsWrongPassword = false;
                            this.ShowLogin = false;
                            this.ShowSettings = true;
                        }

                        catch (WrongPasswordException)
                        {
                            this.IsWrongPassword = true;
                        }
                    },
                    param => !string.IsNullOrWhiteSpace(this.LoginPassword)
                );
            }
        }

        public string LoginPassword { get; set; }

        public ICommand OpenLinkCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => Process.Start((string)param)
                );
            }
        }

        public int PlaylistTimeout
        {
            get { return (int)this.library.PlaylistTimeout.TotalSeconds; }
            set { this.library.PlaylistTimeout = TimeSpan.FromSeconds(value); }
        }

        public string ReleaseNotes
        {
            get { return "http://espera.flagbug.com/release-notes"; }
        }

        public ICommand ReportBugCommand
        {
            get
            {
                return new RelayCommand(param => this.windowManager.ShowWindow(new BugReportViewModel()));
            }
        }

        public bool ShowLogin
        {
            get { return this.showLogin; }
            set
            {
                if (this.ShowLogin != value)
                {
                    this.showLogin = value;
                    this.NotifyOfPropertyChange(() => this.ShowLogin);
                }
            }
        }

        public bool ShowSettings
        {
            get { return this.showSettings; }
            set
            {
                if (this.ShowSettings != value)
                {
                    this.showSettings = value;
                    this.NotifyOfPropertyChange(() => this.ShowSettings);
                }
            }
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
            if (this.IsAdminCreated && this.library.AccessMode == AccessMode.Party)
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