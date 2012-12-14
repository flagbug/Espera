using Espera.Services;
using Rareform.Patterns.MVVM;
using ReactiveUI;
using System;
using System.Reflection;
using System.Windows.Input;

namespace Espera.View.ViewModels
{
    internal class BugReportViewModel : ReactiveObject
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
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded; }
            set { this.RaiseAndSetIfChanged(value); }
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
                            FogBugzService.SubmitCrashReport("Version " + this.version + "\n\n" + this.Message, String.Empty);

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