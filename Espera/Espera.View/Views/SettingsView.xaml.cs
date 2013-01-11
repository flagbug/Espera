using Espera.View.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Espera.View.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void CreateAdminButtonClick(object sender, RoutedEventArgs e)
        {
            ICommand command = ((SettingsViewModel)this.DataContext).CreateAdminCommand;

            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            this.AdminPasswordBox.Password = String.Empty;
        }

        private void CreationPasswordChanged(object sender, RoutedEventArgs e)
        {
            ((SettingsViewModel)this.DataContext).CreationPassword = ((PasswordBox)sender).Password;
        }
    }
}