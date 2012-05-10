using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Espera.Core;
using Espera.View.Properties;
using Espera.View.ViewModels;
using Ionic.Utils;
using MahApps.Metro;
using ListView = System.Windows.Controls.ListView;

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

            this.ChangeColor(Settings.Default.AccentColor);
        }

        private void ChangeColor(string color)
        {
            ThemeManager.ChangeTheme(this, ThemeManager.DefaultAccents.First(accent => accent.Name == color), Theme.Dark);
        }

        private void AddSongsButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialogEx
            {
                Description = "Choose a folder containing the music that you want to add to the library"
            };

            dialog.ShowDialog();

            string selectedPath = dialog.SelectedPath;

            if (!String.IsNullOrEmpty(selectedPath))
            {
                this.mainViewModel.AddSongs(selectedPath);
            }
        }

        private void SongDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && this.mainViewModel.AddSelectedSongsToPlaylistCommand.CanExecute(null))
            {
                this.mainViewModel.AddSelectedSongsToPlaylistCommand.Execute(null);
            }
        }

        private void MetroWindowClosing(object sender, CancelEventArgs e)
        {
            CoreSettings.Default.Save();
            Settings.Default.Save();

            this.mainViewModel.Dispose();
        }

        private void PlaylistDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.mainViewModel.PlayCommand.CanExecute(null))
            {
                this.mainViewModel.PlayCommand.Execute(true);
            }
        }

        private void CreationPasswordChanged(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.AdministratorViewModel.CreationPassword = ((PasswordBox)sender).Password;
        }

        private void LoginPasswordChanged(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.AdministratorViewModel.LoginPassword = ((PasswordBox)sender).Password;
        }

        private void AdminPanelToggleButtonClick(object sender, RoutedEventArgs e)
        {
            this.showAdministratorPanel = !this.showAdministratorPanel;

            this.adminPanel.Visibility = this.showAdministratorPanel ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RedColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Red");
            Settings.Default.AccentColor = "Red";
        }

        private void GreenColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Green");
            Settings.Default.AccentColor = "Green";
        }

        private void BlueColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Blue");
            Settings.Default.AccentColor = "Blue";
        }

        private void PurpleColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Purple");
            Settings.Default.AccentColor = "Purple";
        }

        private void SearchTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.mainViewModel.StartSearch();
            }

            e.Handled = true;
        }

        private void PlaylistKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (this.mainViewModel.RemoveSelectedPlaylistEntriesCommand.CanExecute(null))
                {
                    this.mainViewModel.RemoveSelectedPlaylistEntriesCommand.Execute(null);
                }
            }

            else if (e.Key == Key.Enter)
            {
                if (this.mainViewModel.PlayCommand.CanExecute(null))
                {
                    this.mainViewModel.PlayCommand.Execute(true);
                }
            }
        }

        private void SongListKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (this.mainViewModel.RemoveSelectedSongsFromLibraryCommand.CanExecute(null))
                {
                    this.mainViewModel.RemoveSelectedSongsFromLibraryCommand.Execute(null);
                }
            }
        }

        private void PlaylistContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (((ListView)sender).Items.IsEmpty)
            {
                e.Handled = true;
            }
        }

        private void SongListContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (((ListView)sender).Items.IsEmpty)
            {
                e.Handled = true;
            }
        }

        private void PlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.mainViewModel.SelectedPlaylistEntries = ((ListView)sender).SelectedItems.Cast<PlaylistEntryViewModel>();
        }

        private void SongListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.mainViewModel.SelectedSongs = ((ListView)sender).SelectedItems.Cast<SongViewModel>();
        }

        private void MetroWindowKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (this.mainViewModel.IsPlaying)
                {
                    if (this.mainViewModel.PauseCommand.CanExecute(null))
                    {
                        this.mainViewModel.PauseCommand.Execute(null);
                    }
                }

                else
                {
                    if (this.mainViewModel.PlayCommand.CanExecute(null))
                    {
                        this.mainViewModel.PlayCommand.Execute(false);
                    }
                }
            }
        }

        private void CreateAdminButtonClick(object sender, RoutedEventArgs e)
        {
            this.adminPasswordBox.Password = String.Empty;
        }

        private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            this.loginPasswordBox.Password = String.Empty;
        }
    }
}