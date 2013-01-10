using Caliburn.Micro;
using Espera.Services;
using Rareform.Patterns.MVVM;
using System;
using System.Reflection;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class BugReportViewModel : PropertyChangedBase
    {
        private readonly string version;
        private string message;
        private bool? sendingSucceeded;

        public BugReportViewModel()
        {
            this.version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public string Message
        {
            get { return this.message; }
            set
            {
                if (this.message != value)
                {
                    this.message = value;
                    this.NotifyOfPropertyChange(() => this.Message);
                }
            }
        }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded; }
            set
            {
                if (this.SendingSucceeded != value)
                {
                    this.sendingSucceeded = value;
                    this.NotifyOfPropertyChange(() => this.SendingSucceeded);
                }
            }
        }

        public ICommand SubmitBugReport
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        try
                        {
                            FogBugzService.SubmitReport("Version " + this.version + "\n\n" + this.Message);

                            this.SendingSucceeded = true;
                        }

                        catch (Exception)
                        {
                            this.SendingSucceeded = false;
                        }
                    },
                    param => !string.IsNullOrWhiteSpace(this.Message) &&
                        (this.SendingSucceeded == null || !this.SendingSucceeded.Value));
            }
        }
    }
}