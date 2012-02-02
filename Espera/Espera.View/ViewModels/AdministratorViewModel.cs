using System.Windows.Input;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View.ViewModels
{
    public class AdministratorViewModel
    {
        private readonly Library library;

        public string CreationPassword { get; set; }

        public ICommand CreateAdminCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => this.library.CreateAdmin(this.CreationPassword)
                );
            }
        }

        public AdministratorViewModel(Library library)
        {
            this.library = library;
        }
    }
}