using Caliburn.Micro;
using Espera.Services;
using Rareform.Patterns.MVVM;
using System;
using System.Reflection;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class CrashViewModel : PropertyChangedBase
    {
        private readonly Exception exception;
        private readonly string version;
        private bool? sendingSucceeded;

        public CrashViewModel(Exception exception)
        {
            this.exception = exception;
            this.version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public string ReportContent
        {
            get { return "Version " + this.version + "\n\n" + this.exception; }
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

        public ICommand SubmitCrashReport
        {
            get
            {
                return new RelayCommand
                (
                    param =>
                    {
                        try
                        {
                            FogBugzService.SubmitReport(this.ReportContent);

                            this.SendingSucceeded = true;
                        }

                        catch (Exception)
                        {
                            this.SendingSucceeded = false;
                        }
                    },
                    param => this.SendingSucceeded == null || !this.SendingSucceeded.Value
                );
            }
        }
    }
}