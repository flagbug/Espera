using System.Reactive.Linq;
using Espera.Core.Analytics;
using ReactiveUI;

namespace Espera.View.ViewModels
{
    internal class BugReportViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool?> sendingSucceeded;
        private string message;

        public BugReportViewModel()
        {
            var canSubmit = this.WhenAnyValue(x => x.Message, x => x.SendingSucceeded,
                (message, succeeded) => !string.IsNullOrWhiteSpace(message) && (succeeded == null || !succeeded.Value));
            SubmitBugReport = ReactiveCommand.CreateAsyncObservable(canSubmit, _ =>
                Observable.Start(() => AnalyticsClient.Instance.RecordBugReport(Message, Email),
                    RxApp.TaskpoolScheduler).Select(__ => true));

            sendingSucceeded = SubmitBugReport
                .Select(x => new bool?(x))
                .ToProperty(this, x => x.SendingSucceeded);
        }

        public string Email { get; set; }

        public string Message
        {
            get => message;
            set => this.RaiseAndSetIfChanged(ref message, value);
        }

        public bool? SendingSucceeded => sendingSucceeded == null ? null : sendingSucceeded.Value;

        public ReactiveCommand<bool> SubmitBugReport { get; }
    }
}