using Espera.Services;
using ReactiveUI;
using ReactiveUI.Xaml;
using System;
using System.Reflection;

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

            IObservable<bool> canSubmitBugReport = this.WhenAny(x => x.Message, x => x.SendingSucceeded,
                (message, succeeded) => !string.IsNullOrWhiteSpace(message.Value) && (succeeded.Value == null || !succeeded.Value.Value));

            this.SubmitBugReport = new ReactiveCommand(canSubmitBugReport);
            this.SubmitBugReport.Subscribe(x =>
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
            });
        }

        public string Message
        {
            get { return this.message; }
            set { this.RaiseAndSetIfChanged(value); }
        }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded; }
            private set { this.RaiseAndSetIfChanged(value); }
        }

        public IReactiveCommand SubmitBugReport { get; private set; }
    }
}