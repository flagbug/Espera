using System;
using System.Windows.Input;
using Espera.Core;
using Espera.Core.Library;
using Rareform.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    public class AdministratorViewModel : ViewModelBase<AdministratorViewModel>
    {
        private readonly Library library;
        private string password;

        public bool StreamYoutube
        {
            get { return CoreSettings.Default.StreamYoutube; }
            set { CoreSettings.Default.StreamYoutube = value; }
        }

        public bool LockVolume
        {
            get { return CoreSettings.Default.LockVolume; }
            set { CoreSettings.Default.LockVolume = value; }
        }

        public bool LockTime
        {
            get { return CoreSettings.Default.LockTime; }
            set { CoreSettings.Default.LockTime = value; }
        }

        public string CreationPassword { get; set; }

        public string LoginPassword { get; set; }

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
                    param => !string.IsNullOrWhiteSpace(this.CreationPassword)
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
                        this.library.ChangeToAdmin(this.LoginPassword);

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
                    }
                );
            }
        }

        public AdministratorViewModel(Library library)
        {
            this.library = library;
        }
    }
}