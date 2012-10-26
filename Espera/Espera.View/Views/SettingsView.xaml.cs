using Espera.View.Properties;
using System.Windows;
using System.Windows.Controls;

namespace Espera.View.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void BlueColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            Settings.Default.AccentColor = "Blue";
        }

        private void GreenColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            Settings.Default.AccentColor = "Green";
        }

        private void PurpleColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            Settings.Default.AccentColor = "Purple";
        }

        private void RedColorButtonButtonClick(object sender, RoutedEventArgs e)
        {
            Settings.Default.AccentColor = "Red";
        }
    }
}