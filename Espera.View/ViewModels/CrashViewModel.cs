using System;
using Espera.Core.Analytics;

namespace Espera.View.ViewModels
{
    internal class CrashViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool?> sendingSucceeded;

        public CrashViewModel(Exception exception)
        {
            ReportContent = exception.ToString();

            SubmitCrashReport = ReactiveCommand.CreateAsyncObservable(this.WhenAnyValue(x => x.SendingSucceeded)
                .Select(x => x == null || !x.Value), _ =>
                Observable.Start(() => AnalyticsClient.Instance.RecordCrash(exception), RxApp.TaskpoolScheduler)
                    .Select(__ => true));

            sendingSucceeded = SubmitCrashReport
                .Select(x => new bool?(x))
                .ToProperty(this, x => x.SendingSucceeded);

            if (AnalyticsClient.Instance.EnableAutomaticReports) SubmitCrashReport.Execute(null);
        }

        public string ReportContent { get; }

        public bool? SendingSucceeded => sendingSucceeded == null ? null : sendingSucceeded.Value;

        public bool SendsAutomatically => AnalyticsClient.Instance.EnableAutomaticReports;

        public ReactiveCommand<bool> SubmitCrashReport { get; }
    }
}