using System;
using Espera.Core.Analytics;
using ReactiveUI;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    internal class BugReportViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool?> sendingSucceeded;
        private string message;

        public BugReportViewModel()
        {
            IObservable<bool> canSubmit = this.WhenAnyValue(x => x.Message, x => x.SendingSucceeded,
                (message, succeeded) => !string.IsNullOrWhiteSpace(message) && (succeeded == null || !succeeded.Value));
            this.SubmitBugReport = ReactiveCommand.CreateAsyncObservable(canSubmit, _ =>
                Observable.Start(() => AnalyticsClient.Instance.RecordBugReport(this.Message, this.Email), RxApp.TaskpoolScheduler).Select(__ => true));

            this.sendingSucceeded = this.SubmitBugReport
                .Select(x => new bool?(x))
                .ToProperty(this, x => x.SendingSucceeded);
        }

        public string Email { get; set; }

        public string Message
        {
            get { return this.message; }
            set { this.RaiseAndSetIfChanged(ref this.message, value); }
        }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded == null ? null : this.sendingSucceeded.Value; }
        }

        public ReactiveCommand<bool> SubmitBugReport { get; private set; }
    }
}