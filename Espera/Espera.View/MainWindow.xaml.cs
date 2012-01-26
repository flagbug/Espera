using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

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
        }

        private void AddSongsButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();

            this.mainViewModel.AddSongs(dialog.SelectedPath);
        }

        private void SongDoubleClick(object sender, MouseButtonEventArgs e)
        {
            this.mainViewModel.AddSelectedSongToPlaylist();
        }
    }
}