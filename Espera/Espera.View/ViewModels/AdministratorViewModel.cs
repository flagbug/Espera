using Caliburn.Micro;
using Espera.Core.Management;
using Rareform.Patterns.MVVM;
using Rareform.Validation;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    public sealed class AdministratorViewModel : PropertyChangedBase
    {
        private readonly Library library;
        private bool isWrongPassword;

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
                        this.NotifyOfPropertyChange(() => this.IsParty);
                        this.NotifyOfPropertyChange(() => this.IsAdmin);
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
                        this.NotifyOfPropertyChange(() => this.IsAdmin);
                    },
                    param => !string.IsNullOrWhiteSpace(this.CreationPassword) && !this.IsAdminCreated
                );
            }
        }

        public string CreationPassword { get; set; }

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
            set
            {
                if (this.IsWrongPassword != value)
                {
                    this.isWrongPassword = value;
                    this.NotifyOfPropertyChange(() => this.IsWrongPassword);
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
                        }

                        catch (WrongPasswordException)
                        {
                            this.IsWrongPassword = true;
                        }

                        this.NotifyOfPropertyChange(() => this.IsAdmin);
                        this.NotifyOfPropertyChange(() => this.IsParty);
                    },
                    param => !string.IsNullOrWhiteSpace(this.LoginPassword)
                );
            }
        }

        public string LoginPassword { get; set; }

        public bool StreamYoutube
        {
            get { return this.library.StreamYoutube; }
            set { this.library.StreamYoutube = value; }
        }
    }
}