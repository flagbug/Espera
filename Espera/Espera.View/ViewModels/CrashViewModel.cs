using Espera.Services;
using Rareform.Patterns.MVVM;
using ReactiveUI;
using System;
using System.Reflection;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class CrashViewModel : ReactiveObject
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
            get { return "Version " + this.version + "\n\n" + this.exception.Message + "\n\n" + exception.StackTrace; }
        }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded; }
            set { this.RaiseAndSetIfChanged(value); }
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
                    },
                    param => this.SendingSucceeded == null || !this.SendingSucceeded.Value
                );
            }
        }
    }
}