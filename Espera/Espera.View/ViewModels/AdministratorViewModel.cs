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
    public sealed class AdministratorViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool> canCreateAdmin;
        private readonly Library library;
        private readonly IWindowManager windowManager;
        private ObservableAsPropertyHelper<bool> canLogin;
        private string creationPassword;
        private bool isWrongPassword;
        private string loginPassword;
        private bool show;

        public AdministratorViewModel(Library library, IWindowManager windowManager)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.library = library;

            this.windowManager = windowManager;

            this.ChangeToPartyCommand = new ReactiveCommand(this.WhenAny(x => x.IsAdminCreated, x => x.Value));
            this.ChangeToPartyCommand.Subscribe(p => this.library.ChangeToParty());

            this.canCreateAdmin = this.WhenAny
            (
                x => x.CreationPassword,
                x => !string.IsNullOrWhiteSpace(x.Value) && !this.IsAdminCreated
            )
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

            Observable.Merge(this.ChangeToPartyCommand, this.CreateAdminCommand, this.LoginCommand)
                .Subscribe(p => this.RaisePropertyChanged(x => x.IsAdmin));

            Observable.Merge(this.ChangeToPartyCommand, this.LoginCommand)
                .Subscribe(p => this.RaisePropertyChanged(x => x.IsParty));
        }

        public bool CanCreateAdmin
        {
            get { return this.canCreateAdmin.Value; }
        }

        public bool CanLogin
        {
            get { return this.canLogin.Value; }
        }

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
            get { return "http://github.com/flagbug/Espera"; }
        }

        public bool IsAdmin
        {
            get { return this.library.AccessMode == AccessMode.Administrator; }
        }

        public bool IsAdminCreated
        {
            get { return this.library.IsAdministratorCreated; }
        }

        public bool IsParty
        {
            get { return this.library.AccessMode == AccessMode.Party; }
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

        public IReactiveCommand ReportBugCommand { get; private set; }

        public bool Show
        {
            get { return this.show; }
            set { this.RaiseAndSetIfChanged(value); }
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
    }
}