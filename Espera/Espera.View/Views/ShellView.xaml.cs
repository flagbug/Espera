using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.View.ViewModels;
using GlobalHotKey;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using ReactiveUI;
using YoutubeExtractor;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListView = System.Windows.Controls.ListView;
using TextBox = System.Windows.Controls.TextBox;

namespace Espera.View.Views
{
    public partial class ShellView : IEnableLogger
    {
        private const int SC_MOVE = 0xF010;
        private const int WM_SYSCOMMAND = 0x0112;
        private HotKeyManager hotKeyManager;
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
                this.WireDragAndDrop();

                try
                {
                    this.RegisterGlobalHotKeys();
                }

                catch (Win32Exception ex)
                {
                    this.Log().ErrorException("Couldn't register hotkeys. " +
                                              "There is probably another instance of Espera running " +
                                              "or another application that requested the global hooks", ex);
                }

                this.Events().KeyUp.Where(x => x.Key == Key.Space)
                    .InvokeCommand(this.shellViewModel, x => x.PauseContinueCommand);

                this.shellViewModel.WhenAnyObservable(x => x.CurrentPlaylist.CurrentPlayingEntry)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(x => this.PlaylistListBox.ScrollIntoView(x));
            };

            this.Loaded += async (sender, args) =>
            {
                var updateViewModel = this.shellViewModel.UpdateViewModel;

                if (updateViewModel.ShowChangelog)
                {
                    var dialog = (SimpleDialog)this.Resources["Changelog"];
                    await this.ShowMetroDialogAsync(dialog);
                }
            };
        }

        private static void ChangeAccent(string accentName)
        {
            Accent accent = ThemeManager.Accents.First(x => x.Name == accentName);
            AppTheme theme = ThemeManager.DetectAppStyle(Application.Current).Item1;
            ThemeManager.ChangeAppStyle(Application.Current, accent, theme);
        }

        private static void ChangeAppTheme(string themeName)
        {
            ThemeManager.ChangeAppTheme(Application.Current, themeName);
        }

        private async void CloseChangelog(object sender, RoutedEventArgs e)
        {
            var dialog = (SimpleDialog)this.Resources["Changelog"];

            await this.HideMetroDialogAsync(dialog);

            var updateViewModel = this.shellViewModel.UpdateViewModel;
            updateViewModel.ChangelogShown();
        }

        private IntPtr HandleWindowMove(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
            case WM_SYSCOMMAND:
                int command = wParam.ToInt32() & 0xfff0;

                if (command == SC_MOVE)
                {
                    // Intercept the move command if we aren't allowed to modify the window
                    handled = !this.shellViewModel.CanModifyWindow;
                }
                break;
            }

            return IntPtr.Zero;
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
                this.hotKeyManager.Dispose();
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

        private void RegisterGlobalHotKeys()
        {
            this.hotKeyManager = new HotKeyManager();
            this.hotKeyManager.Register(Key.MediaNextTrack, ModifierKeys.None);
            this.hotKeyManager.Register(Key.MediaPreviousTrack, ModifierKeys.None);
            this.hotKeyManager.Register(Key.MediaPlayPause, ModifierKeys.None);

            IObservable<Key> keyPressed = Observable.FromEventPattern<KeyPressedEventArgs>(
                    h => this.hotKeyManager.KeyPressed += h,
                    h => this.hotKeyManager.KeyPressed -= h)
                .Select(x => x.EventArgs.HotKey.Key);

            keyPressed.Where(x => x == Key.MediaNextTrack).InvokeCommand(this.shellViewModel, x => x.NextSongCommand);
            keyPressed.Where(x => x == Key.MediaPreviousTrack).InvokeCommand(this.shellViewModel, x => x.PreviousSongCommand);
            keyPressed.Where(x => x == Key.MediaPlayPause).InvokeCommand(this.shellViewModel, x => x.PauseContinueCommand);
        }

        private void SearchTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            // Prevent all keys from bubbling up
            e.Handled = true;
        }

        private void SongDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ICommand command = this.shellViewModel.DefaultPlaybackCommand;

            if (e.LeftButton == MouseButtonState.Pressed && command.CanExecute(null))
            {
                command.Execute(null);
            }
        }

        private void SongKeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ICommand command = this.shellViewModel.DefaultPlaybackCommand;

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

            this.shellViewModel.ViewSettings.WhenAnyValue(x => x.AccentColor)
                .Subscribe(ChangeAccent);

            this.shellViewModel.ViewSettings.WhenAnyValue(x => x.AppTheme)
                .Subscribe(ChangeAppTheme);
        }

        private void WireDragAndDrop()
        {
            const string songSourceFormat = "SongSource";

            this.LocalSongs.Events().MouseMove.Merge(this.YoutubeSongs.Events().MouseMove)
                .Where(x => x.LeftButton == MouseButtonState.Pressed)
                .Subscribe(x => DragDrop.DoDragDrop((DependencyObject)x.Source, songSourceFormat, DragDropEffects.Link));

            // Local songs and YouTube songs
            this.PlaylistListBox.Events().Drop
                .Where(x => x.Data.GetDataPresent(DataFormats.StringFormat) && (string)x.Data.GetData(DataFormats.StringFormat) == songSourceFormat)
                .Select(_ => this.shellViewModel.WhenAnyValue(x => x.CurrentSongSource).Select(x => x.AddToPlaylistCommand))
                .Switch()
                .Where(x => x.CanExecute(null))
                .Subscribe(x => x.Execute(null));

            // YouTube links (e.g from the browser)
            this.PlaylistListBox.Events().Drop
                .Where(x =>
                {
                    string urlDontCare;
                    return x.Data.GetDataPresent(DataFormats.StringFormat) &&
                        DownloadUrlResolver.TryNormalizeYoutubeUrl((string)x.Data.GetData(DataFormats.StringFormat), out urlDontCare);
                })
                .Select(x => this.shellViewModel.DirectYoutubeViewModel.AddDirectYoutubeUrlToPlaylist(new Uri((string)x.Data.GetData(DataFormats.StringFormat))).ToObservable())
                .Concat()
                .Subscribe();
        }

        private void WirePlayer()
        {
            IAudioPlayerCallback callback = this.shellViewModel.AudioPlayerCallback;
            callback.GetTime = () => this.Dispatcher.Invoke(() => this.videoPlayer.Position);
            callback.SetTime = time => this.Dispatcher.Invoke(() => this.videoPlayer.Position = time);

            callback.GetVolume = () => this.Dispatcher.Invoke(() => (float)this.videoPlayer.Volume);
            callback.SetVolume = volume => this.Dispatcher.Invoke(() => this.videoPlayer.Volume = volume);

            callback.LoadRequest = path => this.Dispatcher.InvokeAsync(() => this.videoPlayer.Source = path).Task;
            callback.PauseRequest = () => this.Dispatcher.InvokeAsync(() => this.videoPlayer.Pause()).Task;
            callback.PlayRequest = () => this.Dispatcher.InvokeAsync(() => this.videoPlayer.Play()).Task;
            callback.StopRequest = () => this.Dispatcher.InvokeAsync(() => this.videoPlayer.Stop()).Task;
        }

        private void WireScreenStateUpdater()
        {
            ShellViewModel vm = this.shellViewModel;

            vm.UpdateScreenState
                .Skip(1)
                .Where(_ => vm.ViewSettings.LockWindow && vm.ViewSettings.GoFullScreenOnLock)
                .Subscribe(x =>
                {
                    this.IgnoreTaskbarOnMaximize = x == AccessPermission.Guest && vm.ViewSettings.GoFullScreenOnLock;

                    this.WindowState = WindowState.Normal;
                    this.WindowState = WindowState.Maximized;

                    this.Topmost = this.IgnoreTaskbarOnMaximize;
                });

            // Register the window move intercept to prevent the window being resizable by dragging
            // it down on the titlebar
            this.Events().SourceInitialized.FirstAsync().Subscribe(_ =>
            {
                var helper = new WindowInteropHelper(this);
                HwndSource source = HwndSource.FromHwnd(helper.Handle);
                source.AddHook(HandleWindowMove);
            });
        }
    }
}