using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
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
        public MainWindow()
        {
            InitializeComponent();

            ThemeManager.ChangeTheme(this, ThemeManager.DefaultAccents.First(a => a.Name == "Red"), Theme.Dark);
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
            this.mainViewModel.AddSelectedSongToPlaylist();
        }

        private void MetroWindowClosing(object sender, CancelEventArgs e)
        {
            this.mainViewModel.Dispose();
        }
    }
}