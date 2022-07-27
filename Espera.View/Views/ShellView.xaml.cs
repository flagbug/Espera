using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Espera.Core;
using Espera.Core.Audio;
using Espera.Core.Management;
using Espera.Core.Settings;
using Espera.View.ViewModels;

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

            DataContextChanged += (sender, args) =>
            {
                WireDataContext();
                WirePlayer();
                WireScreenStateUpdater();
                WireDragAndDrop();
                WireTaskbarButtons();

                try
                {
                    RegisterGlobalHotKeys();
                }

                catch (Win32Exception ex)
                {
                    this.Log().ErrorException("Couldn't register hotkeys. " +
                                              "There is probably another instance of Espera running " +
                                              "or another application that requested the global hooks", ex);
                }

                WireShortcuts();

                shellViewModel.WhenAnyObservable(x => x.CurrentPlaylist.CurrentPlayingEntry)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(x => PlaylistListBox.ScrollIntoView(x));

                shellViewModel.LocalViewModel.OpenTagEditor.Subscribe(_ => OpenTagEditor());
            };

            Loaded += async (sender, args) =>
            {
                var updateViewModel = shellViewModel.UpdateViewModel;

                if (updateViewModel.ShowChangelog)
                {
                    var dialog = (CustomDialog)Resources["Changelog"];
                    await this.ShowMetroDialogAsync(dialog);
                }

                updateViewModel.DismissUpdateNotification();
            };
        }

        private static void ChangeAccent(string accentName)
        {
            var accent = ThemeManager.Accents.First(x => x.Name == accentName);
            var theme = ThemeManager.DetectAppStyle(Application.Current).Item1;
            ThemeManager.ChangeAppStyle(Application.Current, accent, theme);
        }

        private static void ChangeAppTheme(string themeName)
        {
            ThemeManager.ChangeAppTheme(Application.Current, themeName);
        }

        private async void CloseChangelog(object sender, RoutedEventArgs e)
        {
            var dialog = (CustomDialog)Resources["Changelog"];

            await this.HideMetroDialogAsync(dialog);
        }

        private IntPtr HandleWindowMove(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_SYSCOMMAND:
                    var command = wParam.ToInt32() & 0xfff0;

                    if (command == SC_MOVE)
                        // Intercept the move command if we aren't allowed to modify the window
                        handled = !shellViewModel.CanModifyWindow;
                    break;
            }

            return IntPtr.Zero;
        }

        private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            ICommand command = shellViewModel.SettingsViewModel.LoginCommand;

            if (command.CanExecute(null)) command.Execute(null);

            LoginPasswordBox.Password = string.Empty;
        }

        private void LoginPasswordChanged(object sender, RoutedEventArgs e)
        {
            shellViewModel.SettingsViewModel.LoginPassword = ((PasswordBox)sender).Password;
        }

        private void MainWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            // We want to lose the focus of a textbox when the user clicks anywhere in the application
            mainGrid.Focusable = true;
            mainGrid.Focus();
            mainGrid.Focusable = false;
        }

        private void MetroWindowClosing(object sender, CancelEventArgs e)
        {
            if (shellViewModel.CanModifyWindow)
            {
                shellViewModel.Dispose();
                hotKeyManager.Dispose();
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
                var result = await this.ShowMessageAsync("Save Metadata",
                    "Do you really want to change the metadata of multiple songs?",
                    MessageDialogStyle.AffirmativeAndNegative,
                    new MetroDialogSettings { AffirmativeButtonText = "Save", NegativeButtonText = "Cancel" });

                return result == MessageDialogResult.Affirmative;
            };

            var songs = shellViewModel.LocalViewModel.SelectedSongs.Select(x => (LocalSong)x.Model).ToList();
            var editorViewModel = new TagEditorViewModel(songs, multipleEditWarning);
            TagEditor.Content = new TagEditorView
            {
                DataContext = editorViewModel
            };

            editorViewModel.Finished.FirstAsync()
                .Subscribe(_ => TagEditorFlyout.IsOpen = false);

            TagEditorFlyout.IsOpen = true;
        }

        private void PlaylistContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (((ListBox)sender).Items.IsEmpty) e.Handled = true;
        }

        private void PlaylistDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (shellViewModel.PlayOverrideCommand.CanExecute(null)) shellViewModel.PlayOverrideCommand.Execute(null);
        }

        private void PlaylistKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (shellViewModel.CurrentPlaylist.RemoveSelectedPlaylistEntriesCommand.CanExecute(null))
                    shellViewModel.CurrentPlaylist.RemoveSelectedPlaylistEntriesCommand.Execute(null);

                e.Handled = true;
            }

            else if (e.Key == Key.Enter)
            {
                if (shellViewModel.PlayOverrideCommand.CanExecute(null))
                    shellViewModel.PlayOverrideCommand.Execute(null);

                e.Handled = true;
            }
        }

        private void PlaylistNameTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;

            textBox.CaretIndex = textBox.Text.Length;
        }

        private void PlaylistNameTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) shellViewModel.CurrentEditedPlaylist.EditName = false;

            e.Handled = true; // Don't send key events when renaming a playlist
        }

        private void PlaylistNameTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var playlist = shellViewModel.CurrentEditedPlaylist;

            if (playlist != null) playlist.EditName = false;
        }

        private void PlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            shellViewModel.CurrentPlaylist.SelectedEntries =
                ((ListBox)sender).SelectedItems.Cast<PlaylistEntryViewModel>();
        }

        private void RegisterGlobalHotKeys()
        {
            hotKeyManager = new HotKeyManager();
            hotKeyManager.Register(Key.MediaNextTrack, ModifierKeys.None);
            hotKeyManager.Register(Key.MediaPreviousTrack, ModifierKeys.None);
            hotKeyManager.Register(Key.MediaPlayPause, ModifierKeys.None);

            var keyPressed = Observable.FromEventPattern<KeyPressedEventArgs>(
                    h => hotKeyManager.KeyPressed += h,
                    h => hotKeyManager.KeyPressed -= h)
                .Select(x => x.EventArgs.HotKey.Key);

            keyPressed.Where(x => x == Key.MediaNextTrack).InvokeCommand(shellViewModel, x => x.NextSongCommand);
            keyPressed.Where(x => x == Key.MediaPreviousTrack)
                .InvokeCommand(shellViewModel, x => x.PreviousSongCommand);
            keyPressed.Where(x => x == Key.MediaPlayPause).InvokeCommand(shellViewModel, x => x.PauseContinueCommand);
        }

        private void WireDataContext()
        {
            shellViewModel = (ShellViewModel)DataContext;

            shellViewModel.ViewSettings.WhenAnyValue(x => x.AccentColor)
                .Subscribe(ChangeAccent);

            shellViewModel.ViewSettings.WhenAnyValue(x => x.AppTheme)
                .Subscribe(ChangeAppTheme);
        }

        private void WireDragAndDrop()
        {
            var playlistDropEvent = PlaylistListBox.ItemContainerStyle
                .RegisterEventSetter<DragEventArgs>(DropEvent, x => new DragEventHandler(x))
                .Merge(PlaylistListBox.Events().Drop.Select(x => Tuple.Create((object)null, x)));

            // Local, YouTube and SoundCloud songs
            playlistDropEvent
                .Where(x => x.Item2.Data.GetDataPresent(DataFormats.StringFormat) &&
                            (string)x.Item2.Data.GetData(DataFormats.StringFormat) == DragDropHelper.SongSourceFormat)
                .Subscribe(x =>
                {
                    var targetIndex = x.Item1 == null
                        ? (int?)null
                        : ((PlaylistEntryViewModel)((ListBoxItem)x.Item1).DataContext).Index;

                    var addCommand = shellViewModel.CurrentSongSource.AddToPlaylistCommand;
                    if (addCommand.CanExecute(null)) addCommand.Execute(targetIndex);

                    x.Item2.Handled = true;
                });

            // YouTube links (e.g from the browser)
            playlistDropEvent
                .Where(x => x.Item2.Data.GetDataPresent(DataFormats.StringFormat))
                .Where(x =>
                {
                    var url = (string)x.Item2.Data.GetData(DataFormats.StringFormat);
                    Uri uriResult;
                    var result = Uri.TryCreate(url, UriKind.Absolute, out uriResult) &&
                                 (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                    string urlDontCare;

                    return result && DownloadUrlResolver.TryNormalizeYoutubeUrl(url, out urlDontCare);
                })
                .SelectMany(async x =>
                {
                    var url = (string)x.Item2.Data.GetData(DataFormats.StringFormat);
                    var targetIndex = x.Item1 == null
                        ? (int?)null
                        : ((PlaylistEntryViewModel)((ListBoxItem)x.Item1).DataContext).Index;

                    if (shellViewModel.DirectYoutubeViewModel.AddToPlaylistCommand.CanExecute(null))
                        await shellViewModel.DirectYoutubeViewModel.AddDirectYoutubeUrlToPlaylist(new Uri(url),
                            targetIndex);

                    x.Item2.Handled = true;

                    return Unit.Default;
                }).Subscribe();

            // Moving items inside the playlist
            const string movePlaylistSongFormat = "MovePlaylistSong";

            PlaylistListBox.ItemContainerStyle
                .RegisterEventSetter<MouseEventArgs>(MouseMoveEvent, x => new MouseEventHandler(x))
                .Where(x => x.Item2.LeftButton == MouseButtonState.Pressed &&
                            shellViewModel.CurrentPlaylist.SelectedEntries.Any())
                .Subscribe(x =>
                    DragDrop.DoDragDrop((ListBoxItem)x.Item1, movePlaylistSongFormat, DragDropEffects.Move));

            playlistDropEvent
                .Where(x => x.Item2.Data.GetDataPresent(DataFormats.StringFormat) &&
                            (string)x.Item2.Data.GetData(DataFormats.StringFormat) == movePlaylistSongFormat)
                .Subscribe(x =>
                {
                    if (shellViewModel.CurrentPlaylist.MovePlaylistSongCommand.CanExecute(null))
                    {
                        var targetIndex = x.Item1 == null
                            ? (int?)null
                            : ((PlaylistEntryViewModel)((ListBoxItem)x.Item1).DataContext).Index;

                        shellViewModel.CurrentPlaylist.MovePlaylistSongCommand.Execute(targetIndex);
                    }

                    x.Item2.Handled = true;
                });
        }

        private void WirePlayer()
        {
            var wpfPlayer = new WpfMediaPlayer(videoPlayer);
            shellViewModel.SettingsViewModel.WhenAnyValue(x => x.DefaultPlaybackEngine)
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
                }).Subscribe(x => shellViewModel.RegisterAudioPlayer(x));

            shellViewModel.RegisterVideoPlayer(wpfPlayer);
        }

        private void WireScreenStateUpdater()
        {
            var vm = shellViewModel;

            vm.UpdateScreenState
                .Skip(1)
                .Where(_ => vm.ViewSettings.LockWindow && vm.ViewSettings.GoFullScreenOnLock)
                .Subscribe(x =>
                {
                    IgnoreTaskbarOnMaximize = x == AccessPermission.Guest && vm.ViewSettings.GoFullScreenOnLock;

                    WindowState = WindowState.Normal;
                    WindowState = WindowState.Maximized;

                    Topmost = IgnoreTaskbarOnMaximize;
                });

            // Register the window move intercept to prevent the window being resizable by dragging
            // it down on the titlebar
            this.Events().SourceInitialized.FirstAsync().Subscribe(_ =>
            {
                var helper = new WindowInteropHelper(this);
                var source = HwndSource.FromHwnd(helper.Handle);
                source.AddHook(HandleWindowMove);
            });
        }

        private void WireShortcuts()
        {
            this.Events().KeyUp.Where(x => !(x.OriginalSource is TextBoxBase) && x.Key == Key.Space)
                .InvokeCommand(shellViewModel, x => x.PauseContinueCommand);

            this.Events().KeyDown.Where(x =>
                    x.Key == Key.F && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                .Subscribe(_ => SearchTextBox.Focus());
        }

        private void WireTaskbarButtons()
        {
            shellViewModel.WhenAnyValue(x => x.IsPlaying, x => x ? "Pause" : "Play")
                .BindTo(PauseContinueTaskbarButton, x => x.Description);
            shellViewModel.WhenAnyValue(x => x.IsPlaying, x => x ? "Pause" : "Play")
                .Select(x => string.Format("pack://application:,,,/Espera;component/Images/{0}.png", x))
                .Select(x => BitmapLoader.Current.LoadFromResource(x, null, null).ToObservable())
                .Switch()
                .Select(x => x.ToNative())
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(PauseContinueTaskbarButton, x => x.ImageSource);
        }
    }
}