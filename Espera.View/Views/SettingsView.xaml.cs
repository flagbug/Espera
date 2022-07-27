using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Espera.View.ViewModels;
using Ookii.Dialogs.Wpf;

namespace Espera.View.Views
{
    /// <summary>
    ///     Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView
    {
        public SettingsView()
        {
            InitializeComponent();

            DataContextChanged += (sender, args) =>
            {
                RemotePasswordBox.Password = ((SettingsViewModel)DataContext).RemoteControlPassword;
            };
        }

        private void AddSongSourceButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();

            dialog.ShowDialog();

            if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
                ((SettingsViewModel)DataContext).ChangeLibrarySource(dialog.SelectedPath);
        }

        private void ChangeYoutubeDownloadPath(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();

            dialog.ShowDialog();

            if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
                ((SettingsViewModel)DataContext).YoutubeDownloadPath = dialog.SelectedPath;
        }

        private void CreateAdminButtonClick(object sender, RoutedEventArgs e)
        {
            ICommand command = ((SettingsViewModel)DataContext).CreateAdminCommand;

            if (command.CanExecute(null)) command.Execute(null);

            AdminPasswordBox.Password = string.Empty;
        }

        private void CreationPasswordChanged(object sender, RoutedEventArgs e)
        {
            ((SettingsViewModel)DataContext).CreationPassword = ((PasswordBox)sender).Password;
        }

        private void RemotePasswordChanged(object sender, RoutedEventArgs e)
        {
            ((SettingsViewModel)DataContext).RemoteControlPassword = ((PasswordBox)sender).Password;
        }
    }
}