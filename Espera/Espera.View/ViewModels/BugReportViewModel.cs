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
            this.SubmitBugReport = this.WhenAnyValue(x => x.Message, x => x.SendingSucceeded,
                (message, succeeded) => !string.IsNullOrWhiteSpace(message) && (succeeded == null || !succeeded.Value))
                .ToCommand();

            this.sendingSucceeded = this.SubmitBugReport
                .RegisterAsyncTask(x => AnalyticsClient.Instance.RecordBugReportAsync(this.Message, this.Email))
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

        public IReactiveCommand SubmitBugReport { get; private set; }
    }
}