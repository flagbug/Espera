using Caliburn.Micro;
using Espera.View.Properties;
using Espera.View.ViewModels;

namespace Espera.View
{
    internal class AppBootstrapper : Bootstrapper<ShellViewModel>
    {
        protected override void OnStartup(object sender, System.Windows.StartupEventArgs e)
        {
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }

            base.OnStartup(sender, e);
        }
    }
}