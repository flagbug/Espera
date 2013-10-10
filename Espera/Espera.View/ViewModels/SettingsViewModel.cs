using Caliburn.Micro;
using Espera.Core;
using Espera.Core.Management;
using Espera.Core.Settings;
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
        private readonly CoreSettings coreSettings;
        private readonly Library library;
        private readonly ObservableAsPropertyHelper<string> librarySource;
        private readonly ViewSettings viewSettings;
        private readonly IWindowManager windowManager;
        private string creationPassword;
        private bool isWrongPassword;
        private string loginPassword;
        private bool showLogin;
        private bool showSettings;

        public SettingsViewModel(Library library, ViewSettings viewSettings, CoreSettings coreSettings, IWindowManager windowManager)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            if (viewSettings == null)
                Throw.ArgumentNullException(() => viewSettings);

            if (coreSettings == null)
                Throw.ArgumentNullException(() => coreSettings);

            this.library = library;
            this.viewSettings = viewSettings;
            this.coreSettings = coreSettings;
            this.windowManager = windowManager;

            this.Scaling = 1;

            this.canCreateAdmin = this
                .WhenAnyValue(x => x.CreationPassword, x => !string.IsNullOrWhiteSpace(x) && !this.library.IsAdministratorCreated)
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

            this.canLogin = this.WhenAnyValue(x => x.LoginPassword, x => !string.IsNullOrWhiteSpace(x))
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
            this.ChangeAccentColorCommand.Subscribe(p => this.viewSettings.AccentColor = (string)p);

            this.librarySource = this.library.SongSourcePath.ToProperty(this, x => x.LibrarySource);
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
            get { return this.coreSettings.EnablePlaylistTimeout; }
            set
            {
                this.coreSettings.EnablePlaylistTimeout = value;
                this.RaisePropertyChanged();
            }
        }

        public bool GoFullScreenOnLock
        {
            get { return this.viewSettings.GoFullScreenOnLock; }
            set { this.viewSettings.GoFullScreenOnLock = value; }
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

        public string LibrarySource
        {
            get { return this.librarySource.Value; }
        }

        public bool LockPlaylistRemoval
        {
            get { return this.coreSettings.LockPlaylistRemoval; }
            set { this.coreSettings.LockPlaylistRemoval = value; }
        }

        public bool LockPlaylistSwitching
        {
            get { return this.coreSettings.LockPlaylistSwitching; }
            set { this.coreSettings.LockPlaylistSwitching = value; }
        }

        public bool LockPlayPause
        {
            get { return this.coreSettings.LockPlayPause; }
            set { this.coreSettings.LockPlayPause = value; }
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
            private set { this.RaiseAndSetIfChanged(ref this.showLogin, value); }
        }

        public bool ShowSettings
        {
            get { return this.showSettings; }
            private set { this.RaiseAndSetIfChanged(ref this.showSettings, value); }
        }

        public int SongSourceUpdateInterval
        {
            get { return (int)this.coreSettings.SongSourceUpdateInterval.TotalMinutes; }
            set
            {
                this.coreSettings.SongSourceUpdateInterval = TimeSpan.FromMinutes(value);

                this.RaisePropertyChanged();
            }
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
            get { return this.library.YoutubeDownloadPath; }
            set { this.library.YoutubeDownloadPath = value; }
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

        public void ChangeLibrarySource(string source)
        {
            this.library.ChangeSongSourcePath(source);
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