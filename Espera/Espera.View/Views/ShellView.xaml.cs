using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.View.ViewModels;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
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
        private ShellViewModel shellViewModel;

        public ShellView()
        {
            InitializeComponent();

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-us");

            this.DataContextChanged += (sender, args) =>
            {
                this.WireDataContext();
                this.WirePlayer();
                this.WireScreenStateUpdater();

                this.Events().KeyUp.Where(x => x.Key == Key.Space)
                    .InvokeCommand(this.shellViewModel, x => x.PauseContinueCommand);
            };

            this.Loaded += async (sender, args) =>
            {
                if (((ShellViewModel)this.DataContext).UpdateViewModel.IsUpdated)
                {
                    var dialog = (SimpleDialog)this.Resources["Changelog"];

                    await this.ShowMetroDialogAsync(dialog);
                }
            };
        }

        private void ChangeColor(string color)
        {
            ThemeManager.ChangeTheme(this, ThemeManager.DefaultAccents.First(accent => accent.Name == color), Theme.Dark);
        }

        private async void CloseChangelog(object sender, RoutedEventArgs e)
        {
            var dialog = (SimpleDialog)this.Resources["Changelog"];

            await this.HideMetroDialogAsync(dialog);
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
            if (((ListBox)sender).Items.IsEmpty)
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

                e.Handled = true;
            }

            else if (e.Key == Key.Enter)
            {
                if (this.shellViewModel.PlayOverrideCommand.CanExecute(null))
                {
                    this.shellViewModel.PlayOverrideCommand.Execute(null);
                }

                e.Handled = true;
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
            this.shellViewModel.SelectedPlaylistEntries = ((ListBox)sender).SelectedItems.Cast<PlaylistEntryViewModel>();
        }

        private async void SearchTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && this.shellViewModel.IsYoutube)
            {
                await this.shellViewModel.YoutubeViewModel.StartSearchAsync();
            }

            e.Handled = true;
        }

        private void SongDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ICommand command = this.shellViewModel.IsAdmin ? this.shellViewModel.CurrentSongSource.PlayNowCommand : this.shellViewModel.CurrentSongSource.AddToPlaylistCommand;

            if (e.LeftButton == MouseButtonState.Pressed && command.CanExecute(null))
            {
                command.Execute(null);
            }
        }

        private void SongKeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ICommand command = this.shellViewModel.IsAdmin ? this.shellViewModel.CurrentSongSource.PlayNowCommand : this.shellViewModel.CurrentSongSource.AddToPlaylistCommand;

                if (command.CanExecute(null))
                {
                    command.Execute(null);
                }

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
            this.shellViewModel.AudioPlayerCallback.Finished();
        }

        private void WireDataContext()
        {
            this.shellViewModel = (ShellViewModel)this.DataContext;

            this.ChangeColor(this.shellViewModel.ViewSettings.AccentColor);

            this.shellViewModel.ViewSettings.WhenAnyValue(x => x.AccentColor)
                .Subscribe(this.ChangeColor);
        }

        private void WirePlayer()
        {
            IAudioPlayerCallback callback = this.shellViewModel.AudioPlayerCallback;
            callback.GetTime = () => this.Dispatcher.Invoke(() => this.videoPlayer.Position);
            callback.SetTime = time => this.Dispatcher.Invoke(new Action(() => this.videoPlayer.Position = time));

            callback.GetVolume = () => this.Dispatcher.Invoke(() => (float)this.videoPlayer.Volume);
            callback.SetVolume = volume => this.Dispatcher.Invoke(new Action(() => this.videoPlayer.Volume = volume));

            callback.LoadRequest = () => this.Dispatcher.Invoke(new Action(() => this.videoPlayer.Source = callback.Path));
            callback.PauseRequest = () => this.Dispatcher.Invoke(() => this.videoPlayer.Pause());
            callback.PlayRequest = () => this.Dispatcher.Invoke(() => this.videoPlayer.Play());
            callback.StopRequest = () => this.Dispatcher.Invoke(() => this.videoPlayer.Stop());
        }

        private void WireScreenStateUpdater()
        {
            this.shellViewModel.UpdateScreenState.Subscribe(x =>
            {
                if (this.shellViewModel.ViewSettings.LockWindow && this.shellViewModel.ViewSettings.GoFullScreenOnLock)
                {
                    this.IgnoreTaskbarOnMaximize = x == AccessMode.Party && this.shellViewModel.ViewSettings.GoFullScreenOnLock;

                    this.WindowState = WindowState.Normal;
                    this.WindowState = WindowState.Maximized;

                    this.Topmost = this.IgnoreTaskbarOnMaximize;
                }
            });
        }
    }
}