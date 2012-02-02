using System.Windows.Input;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    public class AdministratorViewModel : ViewModelBase<AdministratorViewModel>
    {
        private readonly Library library;

        public string CreationPassword { get; set; }

        public bool IsAdminCreated
        {
            get { return this.library.IsAdministratorCreated; }
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
                    },
                    param => !string.IsNullOrWhiteSpace(this.CreationPassword)
                );
            }
        }

        public AdministratorViewModel(Library library)
        {
            this.library = library;
        }
    }
}