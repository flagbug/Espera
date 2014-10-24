using System.Windows;
using System.Windows.Controls;

namespace Espera.View.Views
{
    public partial class NetworkSongDetailView
    {
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register("Header", typeof(string), typeof(UserControl));

        public NetworkSongDetailView()
        {
            InitializeComponent();
        }

        public string Header
        {
            get { return (string)this.GetValue(HeaderProperty); }
            set { this.SetValue(HeaderProperty, value); }
        }
    }
}