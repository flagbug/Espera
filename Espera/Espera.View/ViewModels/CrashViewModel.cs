using Espera.Services;
using ReactiveUI;
using ReactiveUI.Xaml;
using System;
using System.Reactive.Linq;
using System.Reflection;

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

            this.SubmitCrashReport = new ReactiveCommand(
                this.WhenAny(x => x.SendingSucceeded, x => x.Value)
                .Select(x => x == null || !x.Value));

            this.SubmitCrashReport.Subscribe(x =>
            {
                try
                {
                    FogBugzService.SubmitCrashReport(this.exception.Message, this.exception.StackTrace);

                    this.sendingSucceeded = true;
                }

                catch (Exception)
                {
                    this.SendingSucceeded = false;
                }
            });
        }

        public string ReportContent
        {
            get { return "Version " + this.version + "\n\n" + this.exception.Message + "\n\n" + exception.StackTrace; }
        }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded; }
            private set { this.RaiseAndSetIfChanged(value); }
        }

        public IReactiveCommand SubmitCrashReport { get; private set; }
    }
}