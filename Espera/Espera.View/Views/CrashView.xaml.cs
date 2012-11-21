using System.Windows;

namespace Espera.View.Views
{
    public partial class CrashView
    {
        public CrashView()
        {
            InitializeComponent();
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}