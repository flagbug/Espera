using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        public MainWindow()
        {
            InitializeComponent();

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-us");

            this.ChangeColor(Settings.Default.AccentColor);
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
                this.mainViewModel.LocalViewModel.AddSongs(selectedPath);
            }
        }

        private void BlueColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Blue");
            Settings.Default.AccentColor = "Blue";
        }

        private void ChangeColor(string color)
        {
            ThemeManager.ChangeTheme(this, ThemeManager.DefaultAccents.First(accent => accent.Name == color), Theme.Dark);
        }

        private void CreateAdminButtonClick(object sender, RoutedEventArgs e)
        {
            ICommand command = this.mainViewModel.AdministratorViewModel.CreateAdminCommand;

            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            this.adminPasswordBox.Password = String.Empty;
        }

        private void CreationPasswordChanged(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.AdministratorViewModel.CreationPassword = ((PasswordBox)sender).Password;
        }

        private void GreenColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Green");
            Settings.Default.AccentColor = "Green";
        }

        private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            ICommand command = this.mainViewModel.AdministratorViewModel.LoginCommand;
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            this.loginPasswordBox.Password = String.Empty;
        }

        private void LoginPasswordChanged(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.AdministratorViewModel.LoginPassword = ((PasswordBox)sender).Password;
        }

        private void MainWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            // We want to lose the focus of a textbox when the user clicks anywhere in the application
            this.mainGrid.Focus();
        }

        private void MetroWindowClosing(object sender, CancelEventArgs e)
        {
            Settings.Default.Save();

            this.mainViewModel.Dispose();
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

        private void PlaylistContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (((ListView)sender).Items.IsEmpty)
            {
                e.Handled = true;
            }
        }

        private void PlaylistDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.mainViewModel.PlayCommand.CanExecute(null))
            {
                this.mainViewModel.PlayCommand.Execute(true);
            }
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

        private void PlaylistNameTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = ((TextBox)sender);

            textBox.CaretIndex = textBox.Text.Length;
        }

        private void PlaylistNameTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.mainViewModel.CurrentEditedPlaylist.EditName = false;
            }
        }

        private void PlaylistNameTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            PlaylistViewModel playlist = this.mainViewModel.CurrentEditedPlaylist;

            if (playlist != null)
            {
                playlist.EditName = false;
            }
        }

        private void PlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.mainViewModel.SelectedPlaylistEntries = ((ListView)sender).SelectedItems.Cast<PlaylistEntryViewModel>();
        }

        private void PlaylistsKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var command = this.mainViewModel.RemovePlaylistCommand;

                if (command.CanExecute(null))
                {
                    command.Execute(null);
                }
            }
        }

        private void PurpleColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Purple");
            Settings.Default.AccentColor = "Purple";
        }

        private void RedColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            this.ChangeColor("Red");
            Settings.Default.AccentColor = "Red";
        }

        private void SearchTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.mainViewModel.YoutubeViewModel.StartSearch();
            }

            e.Handled = true;
        }

        private void SongDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ICommand addToPlaylist = this.mainViewModel.CurrentSongSource.AddToPlaylistCommand;

            if (e.LeftButton == MouseButtonState.Pressed && addToPlaylist.CanExecute(null))
            {
                addToPlaylist.Execute(null);
            }
        }

        private void SongListContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (((ListView)sender).Items.IsEmpty)
            {
                e.Handled = true;
            }
        }

        private void SongListKeyUp(object sender, KeyEventArgs e)
        {
            ICommand removeFromLibrary = this.mainViewModel.LocalViewModel.RemoveFromLibraryCommand;

            if (e.Key == Key.Delete)
            {
                if (removeFromLibrary.CanExecute(null))
                {
                    removeFromLibrary.Execute(null);
                }
            }
        }

        private void SongListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.mainViewModel.CurrentSongSource.SelectedSongs = ((ListView)sender).SelectedItems.Cast<SongViewModel>();
        }

        private void SortLocalSongAlbum(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.LocalViewModel.OrderByAlbum();
        }

        private void SortLocalSongArtist(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.LocalViewModel.OrderByArtist();
        }

        private void SortLocalSongDuration(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.LocalViewModel.OrderByDuration();
        }

        private void SortLocalSongGenre(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.LocalViewModel.OrderByGenre();
        }

        private void SortLocalSongTitle(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.LocalViewModel.OrderByTitle();
        }

        private void SortYoutubeSongDuration(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.YoutubeViewModel.OrderByDuration();
        }

        private void SortYoutubeSongRating(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.YoutubeViewModel.OrderByRating();
        }

        private void SortYoutubeSongTitle(object sender, RoutedEventArgs e)
        {
            this.mainViewModel.YoutubeViewModel.OrderByTitle();
        }
    }
}