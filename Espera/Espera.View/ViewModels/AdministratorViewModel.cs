using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using Espera.Core.Management;
using Rareform.Patterns.MVVM;
using Rareform.Validation;

namespace Espera.View.ViewModels
{
    public class AdministratorViewModel : ViewModelBase<AdministratorViewModel>
    {
        private readonly Library library;
        private bool isWrongPassword;
        private bool show;

        public AdministratorViewModel(Library library)
        {
            if (library == null)
                Throw.ArgumentNullException(() => library);

            this.library = library;
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
                        this.OnPropertyChanged(vm => vm.IsParty);
                        this.OnPropertyChanged(vm => vm.IsAdmin);
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

                        this.OnPropertyChanged(vm => vm.IsAdminCreated);
                        this.OnPropertyChanged(vm => vm.IsAdmin);
                    },
                    param => !string.IsNullOrWhiteSpace(this.CreationPassword) && !this.IsAdminCreated
                );
            }
        }

        public string CreationPassword { get; set; }

        public bool EnablePlaylistTimeout
        {
            get { return this.library.EnablePlaylistTimeout; }
            set
            {
                if (this.library.EnablePlaylistTimeout != value)
                {
                    this.library.EnablePlaylistTimeout = value;
                    this.OnPropertyChanged(vm => vm.EnablePlaylistTimeout);
                }
            }
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

        public bool IsVlcInstalled
        {
            get { return RegistryHelper.IsVlcInstalled(); }
        }

        public bool IsWrongPassword
        {
            get { return this.isWrongPassword; }
            set
            {
                if (this.IsWrongPassword != value)
                {
                    this.isWrongPassword = value;
                    this.OnPropertyChanged(vm => vm.IsWrongPassword);
                }
            }
        }

        public bool LockLibraryRemoval
        {
            get { return this.library.LockLibraryRemoval; }
            set { this.library.LockLibraryRemoval = value; }
        }

        public bool LockPlaylistSwitching
        {
            get { return this.library.LockPlaylistSwitching; }
            set { this.library.LockPlaylistSwitching = value; }
        }

        public bool LockPlaylistRemoval
        {
            get { return this.library.LockPlaylistRemoval; }
            set { this.library.LockPlaylistRemoval = value; }
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
                        }

                        catch (InvalidPasswordException)
                        {
                            this.IsWrongPassword = true;
                        }

                        this.OnPropertyChanged(vm => vm.IsAdmin);
                        this.OnPropertyChanged(vm => vm.IsParty);
                    },
                    param => !string.IsNullOrWhiteSpace(this.LoginPassword)
                );
            }
        }

        public string LoginPassword { get; set; }

        public ICommand OpenHomepageCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => Process.Start(this.Homepage)
                );
            }
        }

        public int PlaylistTimeout
        {
            get { return (int)this.library.PlaylistTimeout.TotalSeconds; }
            set { this.library.PlaylistTimeout = TimeSpan.FromSeconds(value); }
        }

        public bool Show
        {
            get { return this.show; }
            set
            {
                if (this.Show != value)
                {
                    this.show = value;
                    this.OnPropertyChanged(vm => vm.Show);
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
    }
}