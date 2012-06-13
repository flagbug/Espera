using System;
using System.Windows.Input;
using Espera.Core.Library;
using Rareform.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    public class AdministratorViewModel : ViewModelBase<AdministratorViewModel>
    {
        private readonly Library library;
        private bool isWrongPassword;

        public bool StreamYoutube
        {
            get { return this.library.StreamYoutube; }
            set { this.library.StreamYoutube = value; }
        }

        public bool LockVolume
        {
            get { return this.library.LockVolume; }
            set { this.library.LockVolume = value; }
        }

        public bool LockTime
        {
            get { return this.library.LockTime; }
            set { this.library.LockTime = value; }
        }

        public bool LockSongRemoval
        {
            get { return this.library.LockSongRemoval; }
            set { this.library.LockSongRemoval = value; }
        }

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

        public int PlaylistTimeout
        {
            get { return (int)this.library.PlaylistTimeout.TotalSeconds; }
            set { this.library.PlaylistTimeout = TimeSpan.FromSeconds(value); }
        }

        public string CreationPassword { get; set; }

        public string LoginPassword { get; set; }

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

        public bool IsAdminCreated
        {
            get { return this.library.IsAdministratorCreated; }
        }

        public bool IsAdmin
        {
            get { return this.library.AccessMode == AccessMode.Administrator; }
        }

        public bool IsParty
        {
            get { return this.library.AccessMode == AccessMode.Party; }
        }

        public bool IsVlcInstalled
        {
            get { return RegistryHelper.IsVlcInstalled(); }
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

        public AdministratorViewModel(Library library)
        {
            this.library = library;
        }
    }
}