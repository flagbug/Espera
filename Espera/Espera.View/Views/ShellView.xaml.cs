using Espera.Core;
using Espera.Core.Management;
using Espera.View.Properties;
using Espera.View.ViewModels;
using Ionic.Utils;
using MahApps.Metro;
using Ookii.Dialogs.Wpf;
using Rareform.Reflection;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListView = System.Windows.Controls.ListView;
using TextBox = System.Windows.Controls.TextBox;

namespace Espera.View.Views
{
    public partial class ShellView
    {
        private IVideoPlayerCallback currentVideoPlayerCallback;
        private ShellViewModel shellViewModel;

        public ShellView()
        {
            InitializeComponent();

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-us");

            this.ChangeColor(Settings.Default.AccentColor);

            this.DataContextChanged += (sender, args) =>
            {
                this.WireDataContext();
                this.WireVideoPlayer();
                this.WireScreenStateUpdater();
            };

            Settings.Default.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == Reflector.GetMemberName(() => Settings.Default.AccentColor))
                {
                    this.ChangeColor(Settings.Default.AccentColor);
                }
            };
        }

        private void AddSongsButtonClick(object sender, RoutedEventArgs e)
        {
            string selectedPath;

            if (VistaFolderBrowserDialog.IsVistaFolderDialogSupported)
            {
                var dialog = new VistaFolderBrowserDialog();

                dialog.ShowDialog(this);

                selectedPath = dialog.SelectedPath;
            }

            else
            {
                using (var dialog = new FolderBrowserDialogEx())
                {
                    dialog.ShowDialog();

                    selectedPath = dialog.SelectedPath;
                }
            }

            if (!String.IsNullOrEmpty(selectedPath))
            {
                this.shellViewModel.LocalViewModel.AddSongs(selectedPath);
            }
        }

        private void ChangeColor(string color)
        {
            ThemeManager.ChangeTheme(this, ThemeManager.DefaultAccents.First(accent => accent.Name == color), Theme.Dark);
        }

        private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            ICommand command = this.shellViewModel.SettingsViewModel.LoginCommand;

            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            this.LoginPasswordBox.Password = String.Empty;
        }

        private void LoginPasswordChanged(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.SettingsViewModel.LoginPassword = ((PasswordBox)sender).Password;
        }

        private void MainWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            // We want to lose the focus of a textbox when the user clicks anywhere in the application
            this.mainGrid.Focusable = true;
            this.mainGrid.Focus();
            this.mainGrid.Focusable = false;
        }

        private void MetroWindowClosing(object sender, CancelEventArgs e)
        {
            if (this.shellViewModel.CanModifyWindow)
            {
                this.shellViewModel.Dispose();
            }

            else
            {
                e.Cancel = true;
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
            if (this.shellViewModel.PlayOverrideCommand.CanExecute(null))
            {
                this.shellViewModel.PlayOverrideCommand.Execute(null);
            }
        }

        private void PlaylistKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (this.shellViewModel.RemoveSelectedPlaylistEntriesCommand.CanExecute(null))
                {
                    this.shellViewModel.RemoveSelectedPlaylistEntriesCommand.Execute(null);
                }
            }

            else if (e.Key == Key.Enter)
            {
                if (this.shellViewModel.PlayOverrideCommand.CanExecute(null))
                {
                    this.shellViewModel.PlayOverrideCommand.Execute(null);
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
                this.shellViewModel.CurrentEditedPlaylist.EditName = false;
            }

            e.Handled = true; // Don't send key events when renaming a playlist
        }

        private void PlaylistNameTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            PlaylistViewModel playlist = this.shellViewModel.CurrentEditedPlaylist;

            if (playlist != null)
            {
                playlist.EditName = false;
            }
        }

        private void PlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.shellViewModel.SelectedPlaylistEntries = ((ListView)sender).SelectedItems.Cast<PlaylistEntryViewModel>();
        }

        private void SearchTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.shellViewModel.YoutubeViewModel.StartSearch();
            }

            e.Handled = true;
        }

        private void SetupVideoPlayer(IVideoPlayerCallback callback)
        {
            this.currentVideoPlayerCallback = callback;
            callback.GetTime = () => this.Dispatcher.Invoke(() => this.videoPlayer.Position);
            callback.SetTime = time => this.Dispatcher.Invoke(new Action(() => this.videoPlayer.Position = time));

            callback.GetVolume = () => this.Dispatcher.Invoke(() => (float)this.videoPlayer.Volume);
            callback.SetVolume = volume => this.Dispatcher.Invoke(new Action(() => this.videoPlayer.Volume = volume));

            callback.LoadRequest = () => this.Dispatcher.Invoke(new Action(() => this.videoPlayer.Source = callback.VideoUrl));
            callback.PauseRequest = () => this.Dispatcher.Invoke(() => this.videoPlayer.Pause());
            callback.PlayRequest = () => this.Dispatcher.Invoke(() => this.videoPlayer.Play());
            callback.StopRequest = () => this.Dispatcher.Invoke(() => this.videoPlayer.Stop());
        }

        private void SongDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ICommand addToPlaylist = this.shellViewModel.CurrentSongSource.AddToPlaylistCommand;

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

        private void SongListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.shellViewModel.CurrentSongSource.SelectedSongs = ((ListView)sender).SelectedItems.Cast<SongViewModelBase>();
        }

        private void SortLocalSongAlbum(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.LocalViewModel.OrderByAlbum();
        }

        private void SortLocalSongArtist(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.LocalViewModel.OrderByArtist();
        }

        private void SortLocalSongDuration(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.LocalViewModel.OrderByDuration();
        }

        private void SortLocalSongGenre(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.LocalViewModel.OrderByGenre();
        }

        private void SortLocalSongTitle(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.LocalViewModel.OrderByTitle();
        }

        private void SortYoutubeSongDuration(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.YoutubeViewModel.OrderByDuration();
        }

        private void SortYoutubeSongRating(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.YoutubeViewModel.OrderByRating();
        }

        private void SortYoutubeSongTitle(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.YoutubeViewModel.OrderByTitle();
        }

        private void SortYoutubeSongViews(object sender, RoutedEventArgs e)
        {
            this.shellViewModel.YoutubeViewModel.OrderByViews();
        }

        private void VideoPlayerMediaEnded(object sender, RoutedEventArgs e)
        {
            this.currentVideoPlayerCallback.Finished();
        }

        private void WireDataContext()
        {
            this.shellViewModel = (ShellViewModel)this.DataContext;
        }

        private void WireScreenStateUpdater()
        {
            this.shellViewModel.UpdateScreenState.Subscribe(x =>
            {
                if (Settings.Default.LockWindow && Settings.Default.GoFullScreenOnLock)
                {
                    this.IgnoreTaskbarOnMaximize = x == AccessMode.Party && Settings.Default.GoFullScreenOnLock;

                    this.WindowState = WindowState.Normal;
                    this.WindowState = WindowState.Maximized;

                    this.Topmost = this.IgnoreTaskbarOnMaximize;
                }
            });
        }

        private void WireVideoPlayer()
        {
            this.shellViewModel.VideoPlayerCallback.Subscribe(this.SetupVideoPlayer);
        }
    }
}