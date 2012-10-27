using Caliburn.Micro;
using Espera.View.Properties;
using Rareform.Patterns.MVVM;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class SettingsViewModel : PropertyChangedBase
    {
        private bool show;

        public ICommand ChangeAccentColorCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => Settings.Default.AccentColor = (string)param
                );
            }
        }

        public string DonationPage
        {
            get { return "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=K5AWR8EDG9QJY"; }
        }

        public string Homepage
        {
            get { return "http://github.com/flagbug/Espera"; }
        }

        public string IssuesPage
        {
            get { return "http://github.com/flagbug/Espera/issues"; }
        }

        public ICommand OpenLinkCommand
        {
            get
            {
                return new RelayCommand
                (
                    param => Process.Start((string)param)
                );
            }
        }

        public bool Show
        {
            get { return this.show; }
            set
            {
                if (this.Show != value)
                {
                    this.show = value;
                    this.NotifyOfPropertyChange(() => this.Show);
                }
            }
        }

        public string Version
        {
            get
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;

                return String.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Revision);
            }
        }
    }
}