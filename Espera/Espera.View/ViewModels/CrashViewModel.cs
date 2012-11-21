using Espera.Services;
using Rareform.Patterns.MVVM;
using System;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class CrashViewModel
    {
        private readonly Exception exception;

        public CrashViewModel(Exception exception)
        {
            this.exception = exception;
        }

        public string ReportContent
        {
            get { return this.exception.Message + "\n\n" + exception.StackTrace; }
        }

        public ICommand SubmitBugReport
        {
            get
            {
                return new RelayCommand
                (
                    param => FogBugzService.SubmitCrashReport(this.exception.Message, this.exception.StackTrace)
                );
            }
        }
    }
}