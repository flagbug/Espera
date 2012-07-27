using System.Windows;
using Espera.View.Properties;

namespace Espera.View
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Settings.Default.Upgrade();

            base.OnStartup(e);
        }
    }
}