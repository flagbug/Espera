using System.Windows;

namespace Espera.View.Views
{
    public partial class BugReportView
    {
        public BugReportView()
        {
            InitializeComponent();
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}