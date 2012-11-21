using Caliburn.Micro;
using Espera.Services;
using Rareform.Patterns.MVVM;
using System;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class CrashViewModel : PropertyChangedBase
    {
        private readonly Exception exception;
        private bool? sendingSucceeded;

        public CrashViewModel(Exception exception)
        {
            this.exception = exception;
        }

        public string ReportContent
        {
            get { return this.exception.Message + "\n\n" + exception.StackTrace; }
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
                            FogBugzService.SubmitCrashReport(this.exception.Message, this.exception.StackTrace);

                            this.SendingSucceeded = true;
                        }

                        catch (Exception)
                        {
                            this.SendingSucceeded = false;
                        }
                    }
                );
            }
        }
    }
}