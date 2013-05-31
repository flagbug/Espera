using Caliburn.Micro;
using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using Rareform.Validation;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private double scaling;
        private bool showLogin;
        private bool showSettings;

        private YoutubeStreamingQuality youtubeStreamingQuality;

        public SettingsViewModel(Library library, IWindowManager windowManager)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.library = library;
            this.windowManager = windowManager;

            this.Scaling = 1;

            this.canCreateAdmin = this
                .WhenAny(x => x.CreationPassword, x => !string.IsNullOrWhiteSpace(x.Value) && !this.library.IsAdministratorCreated)
                .ToProperty(this, x => x.CanCreateAdmin);

            this.CreateAdminCommand = new ReactiveCommand(this.canCreateAdmin, false,
                ImmediateScheduler.Instance); // Immediate execution, because we set the password to an empty string afterwards
            this.CreateAdminCommand.Subscribe(p => this.library.CreateAdmin(this.CreationPassword));

            this.ChangeToPartyCommand = new ReactiveCommand(this.CreateAdminCommand.Select(x => true).StartWith(false));
            this.ChangeToPartyCommand.Subscribe(p =>
            {
                this.library.ChangeToParty();
                this.ShowSettings = false;
            });

            this.canLogin = this.WhenAny(x => x.LoginPassword, x => !string.IsNullOrWhiteSpace(x.Value))
                .ToProperty(this, x => x.CanLogin);

            this.LoginCommand = new ReactiveCommand(this.canLogin, false,
                ImmediateScheduler.Instance); // Immediate execution, because we set the password to an empty string afterwards
            this.LoginCommand.Subscribe(p =>
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
            });

            this.OpenLinkCommand = new ReactiveCommand();
            this.OpenLinkCommand.Subscribe(p => Process.Start((string)p));

            this.ReportBugCommand = new ReactiveCommand();
            this.ReportBugCommand.Subscribe(p => this.windowManager.ShowWindow(new BugReportViewModel()));

            this.ChangeAccentColorCommand = new ReactiveCommand();
            this.ChangeAccentColorCommand.Subscribe(p => Settings.Default.AccentColor = (string)p);
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

        public IReactiveCommand ChangeAccentColorCommand { get; private set; }

        public IReactiveCommand ChangeToPartyCommand { get; private set; }

        public IReactiveCommand CreateAdminCommand { get; private set; }

        public string CreationPassword
        {
            private get { return this.creationPassword; }
            set { this.RaiseAndSetIfChanged(ref this.creationPassword, value); }
        }

        public string DonationPage
        {
            get { return "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=K5AWR8EDG9QJY"; }
        }

        public bool EnablePlaylistTimeout
        {
            get { return this.library.EnablePlaylistTimeout.Value; }
            set
            {
                if (this.library.EnablePlaylistTimeout.Value != value)
                {
                    this.library.EnablePlaylistTimeout.Value = value;

                    this.RaisePropertyChanged();
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

        public bool IsWrongPassword
        {
            get { return this.isWrongPassword; }
            set { this.RaiseAndSetIfChanged(ref this.isWrongPassword, value); }
        }

        public bool LockLibraryRemoval
        {
            get { return this.library.LockLibraryRemoval.Value; }
            set { this.library.LockLibraryRemoval.Value = value; }
        }

        public bool LockPlaylistRemoval
        {
            get { return this.library.LockPlaylistRemoval.Value; }
            set { this.library.LockPlaylistRemoval.Value = value; }
        }

        public bool LockPlaylistSwitching
        {
            get { return this.library.LockPlaylistSwitching.Value; }
            set { this.library.LockPlaylistSwitching.Value = value; }
        }

        public bool LockPlayPause
        {
            get { return this.library.LockPlayPause.Value; }
            set { this.library.LockPlayPause.Value = value; }
        }

        public bool LockTime
        {
            get { return this.library.LockTime.Value; }
            set { this.library.LockTime.Value = value; }
        }

        public bool LockVolume
        {
            get { return this.library.LockVolume.Value; }
            set { this.library.LockVolume.Value = value; }
        }

        public bool LockWindow
        {
            get { return Settings.Default.LockWindow; }
            set
            {
                if (this.LockWindow != value)
                {
                    Settings.Default.LockWindow = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public IReactiveCommand LoginCommand { get; private set; }

        public string LoginPassword
        {
            private get { return this.loginPassword; }
            set { this.RaiseAndSetIfChanged(ref this.loginPassword, value); }
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

        public double Scaling
        {
            get { return this.scaling; }
            set { this.RaiseAndSetIfChanged(ref this.scaling, value); }
        }

        public bool ShowLogin
        {
            get { return this.showLogin; }
            private set { this.RaiseAndSetIfChanged(ref this.showLogin, value); }
        }

        public bool ShowSettings
        {
            get { return this.showSettings; }
            private set { this.RaiseAndSetIfChanged(ref this.showSettings, value); }
        }

        public bool StreamHighestYoutubeQuality
        {
            get { return this.library.StreamHighestYoutubeQuality; }
            set
            {
                this.library.StreamHighestYoutubeQuality = value;

                this.RaisePropertyChanged();
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

        public YoutubeStreamingQuality YoutubeStreamingQuality
        {
            get { return this.library.YoutubeStreamingQuality; }
            set
            {
                this.library.YoutubeStreamingQuality = value;
                this.RaisePropertyChanged();
            }
        }

        public void HandleSettings()
        {
            if (this.library.IsAdministratorCreated && this.library.AccessMode.FirstAsync().Wait() == AccessMode.Party)
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