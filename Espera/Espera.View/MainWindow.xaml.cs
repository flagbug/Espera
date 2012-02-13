using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using MahApps.Metro;

namespace Espera.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private bool showAdministratorPanel;

        public MainWindow()
        {
            InitializeComponent();

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-us");

            this.ChangeColor("Blue");
        }

        private void ChangeColor(string color)
        {
            ThemeManager.ChangeTheme(this, ThemeManager.DefaultAccents.First(accent => accent.Name == color), Theme.Dark);
        }

        private void AddSongsButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();

            string selectedPath = dialog.SelectedPath;

            if (!String.IsNullOrEmpty(selectedPath))
            {
                this.mainViewModel.AddSongs(selectedPath);
            }
        }

        private void SongDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.mainViewModel.IsAdmin)
            {
                this.mainViewModel.AddSelectedSongToPlaylist();
            }
        }

        private void MetroWindowClosing(object sender, CancelEventArgs e)
        {
            this.mainViewModel.Dispose();
        }

        private void PlaylistDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.mainViewModel.IsAdmin)
            {
                this.mainViewModel.PlayCommand.Execute(null);
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.AdministratorViewModel.Password = ((PasswordBox)sender).Password;
        }

        private void AdminPanelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            this.showAdministratorPanel = !this.showAdministratorPanel;

            this.adminPanel.Visibility = this.showAdministratorPanel ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RedColorButtonButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Red");
        }

        private void GreenColorButtonButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Green");
        }

        private void BlueColorButtonButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Blue");
        }

        private void SearchTextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.mainViewModel.StartSearch();
                e.Handled = true;
            }
        }
    }
}