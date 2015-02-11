using Espera.Core;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.View.ViewModels;
using GlobalHotKey;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using ReactiveUI;
using Splat;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using YoutubeExtractor;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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
                this.WireTaskbarButtons();

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

                this.WireShortcuts();

                this.shellViewModel.WhenAnyObservable(x => x.CurrentPlaylist.CurrentPlayingEntry)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(x => this.PlaylistListBox.ScrollIntoView(x));

                this.shellViewModel.LocalViewModel.OpenTagEditor.Subscribe(_ => this.OpenTagEditor());
            };

            this.Loaded += async (sender, args) =>
            {
                var updateViewModel = this.shellViewModel.UpdateViewModel;

                if (updateViewModel.ShowChangelog)
                {
                    var dialog = (CustomDialog)this.Resources["Changelog"];
                    await this.ShowMetroDialogAsync(dialog);
                }

                updateViewModel.DismissUpdateNotification();
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
            var dialog = (CustomDialog)this.Resources["Changelog"];

            await this.HideMetroDialogAsync(dialog);
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

        private void OpenTagEditor()
        {
            Func<Task<bool>> multipleEditWarning = async () =>
            {
                MessageDialogResult result = await this.ShowMessageAsync("Save Metadata", "Do you really want to change the metadata of multiple songs?",
                    MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings { AffirmativeButtonText = "Save", NegativeButtonText = "Cancel" });

                return result == MessageDialogResult.Affirmative;
            };

            var songs = this.shellViewModel.LocalViewModel.SelectedSongs.Select(x => (LocalSong)x.Model).ToList();
            var editorViewModel = new TagEditorViewModel(songs, multipleEditWarning);
            this.TagEditor.Content = new TagEditorView
            {
                DataContext = editorViewModel
            };

            editorViewModel.Finished.FirstAsync()
                .Subscribe(_ => this.TagEditorFlyout.IsOpen = false);

            this.TagEditorFlyout.IsOpen = true;
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
                if (this.shellViewModel.CurrentPlaylist.RemoveSelectedPlaylistEntriesCommand.CanExecute(null))
                {
                    this.shellViewModel.CurrentPlaylist.RemoveSelectedPlaylistEntriesCommand.Execute(null);
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
            this.shellViewModel.CurrentPlaylist.SelectedEntries = ((ListBox)sender).SelectedItems.Cast<PlaylistEntryViewModel>();
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
            var playlistDropEvent = this.PlaylistListBox.ItemContainerStyle.RegisterEventSetter<DragEventArgs>(DropEvent, x => new DragEventHandler(x))
                .Merge(this.PlaylistListBox.Events().Drop.Select(x => Tuple.Create((object)null, x)));

            // Local, YouTube and SoundCloud songs
            playlistDropEvent
                .Where(x => x.Item2.Data.GetDataPresent(DataFormats.StringFormat) && (string)x.Item2.Data.GetData(DataFormats.StringFormat) == DragDropHelper.SongSourceFormat)
                .Subscribe(x =>
                {
                    int? targetIndex = x.Item1 == null ? (int?)null : ((PlaylistEntryViewModel)((ListBoxItem)(x.Item1)).DataContext).Index;

                    var addCommand = this.shellViewModel.CurrentSongSource.AddToPlaylistCommand;
                    if (addCommand.CanExecute(null))
                    {
                        addCommand.Execute(targetIndex);
                    }

                    x.Item2.Handled = true;
                });

            // YouTube links (e.g from the browser)
            playlistDropEvent
                .Where(x => x.Item2.Data.GetDataPresent(DataFormats.StringFormat))
                .Where(x =>
                {
                    var url = (string)x.Item2.Data.GetData(DataFormats.StringFormat);
                    Uri uriResult;
                    bool result = Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                    string urlDontCare;

                    return result && DownloadUrlResolver.TryNormalizeYoutubeUrl(url, out urlDontCare);
                })
                .SelectMany(async x =>
                {
                    var url = (string)x.Item2.Data.GetData(DataFormats.StringFormat);
                    int? targetIndex = x.Item1 == null ? (int?)null : ((PlaylistEntryViewModel)((ListBoxItem)(x.Item1)).DataContext).Index;

                    if (this.shellViewModel.DirectYoutubeViewModel.AddToPlaylistCommand.CanExecute(null))
                    {
                        await this.shellViewModel.DirectYoutubeViewModel.AddDirectYoutubeUrlToPlaylist(new Uri(url), targetIndex);
                    }

                    x.Item2.Handled = true;

                    return Unit.Default;
                }).Subscribe();

            // Moving items inside the playlist
            const string movePlaylistSongFormat = "MovePlaylistSong";

            this.PlaylistListBox.ItemContainerStyle.RegisterEventSetter<MouseEventArgs>(MouseMoveEvent, x => new MouseEventHandler(x))
                .Where(x => x.Item2.LeftButton == MouseButtonState.Pressed && this.shellViewModel.CurrentPlaylist.SelectedEntries.Any())
                .Subscribe(x => DragDrop.DoDragDrop((ListBoxItem)x.Item1, movePlaylistSongFormat, DragDropEffects.Move));

            playlistDropEvent
                .Where(x => x.Item2.Data.GetDataPresent(DataFormats.StringFormat) && (string)x.Item2.Data.GetData(DataFormats.StringFormat) == movePlaylistSongFormat)
                .Subscribe(x =>
                {
                    if (this.shellViewModel.CurrentPlaylist.MovePlaylistSongCommand.CanExecute(null))
                    {
                        int? targetIndex = x.Item1 == null ? (int?)null : ((PlaylistEntryViewModel)((ListBoxItem)(x.Item1)).DataContext).Index;

                        this.shellViewModel.CurrentPlaylist.MovePlaylistSongCommand.Execute(targetIndex);
                    }

                    x.Item2.Handled = true;
                });
        }

        private void WirePlayer()
        {
            var wpfPlayer = new WpfMediaPlayer(this.videoPlayer);
            this.shellViewModel.SettingsViewModel.WhenAnyValue(x => x.DefaultPlaybackEngine)
                .Select<DefaultPlaybackEngine, IMediaPlayerCallback>(x =>
                {
                    switch (x)
                    {
                        case DefaultPlaybackEngine.NAudio:
                            return new NAudioMediaPlayer();

                        case DefaultPlaybackEngine.Wpf:
                            return wpfPlayer;
                    }

                    throw new NotImplementedException();
                }).Subscribe(x => this.shellViewModel.RegisterAudioPlayer(x));

            this.shellViewModel.RegisterVideoPlayer(wpfPlayer);
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

        private void WireShortcuts()
        {
            this.Events().KeyUp.Where(x => !(x.OriginalSource is TextBoxBase) && x.Key == Key.Space)
                .InvokeCommand(this.shellViewModel, x => x.PauseContinueCommand);

            this.Events().KeyDown.Where(x => x.Key == Key.F && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                .Subscribe(_ => this.SearchTextBox.Focus());
        }

        private void WireTaskbarButtons()
        {
            this.shellViewModel.WhenAnyValue(x => x.IsPlaying, x => x ? "Pause" : "Play")
                .BindTo(this.PauseContinueTaskbarButton, x => x.Description);
            this.shellViewModel.WhenAnyValue(x => x.IsPlaying, x => x ? "Pause" : "Play")
                .Select(x => String.Format("pack://application:,,,/Espera;component/Images/{0}.png", x))
                .Select(x => BitmapLoader.Current.LoadFromResource(x, null, null).ToObservable())
                .Switch()
                .Select(x => x.ToNative())
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(this.PauseContinueTaskbarButton, x => x.ImageSource);
        }
    }
}