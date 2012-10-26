using Espera.View.Properties;
using Rareform.Patterns.MVVM;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class SettingsViewModel
    {
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
    }
}