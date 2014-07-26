using Espera.Core.Analytics;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using ReactiveUI.Legacy;

namespace Espera.View.ViewModels
{
    internal class CrashViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool?> sendingSucceeded;

        public CrashViewModel(Exception exception)
        {
            this.ReportContent = exception.ToString();

            this.SubmitCrashReport = new ReactiveUI.Legacy.ReactiveCommand(this.WhenAnyValue(x => x.SendingSucceeded)
                .Select(x => x == null || !x.Value));

            this.sendingSucceeded = this.SubmitCrashReport
                .RegisterAsyncTask(x => AnalyticsClient.Instance.RecordCrashAsync(exception))
                .Select(x => new bool?(x))
                .ToProperty(this, x => x.SendingSucceeded);

            if (AnalyticsClient.Instance.EnableAutomaticReports)
            {
                this.SubmitCrashReport.Execute(null);
            }
        }

        public string ReportContent { get; private set; }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded == null ? null : this.sendingSucceeded.Value; }
        }

        public bool SendsAutomatically
        {
            get { return AnalyticsClient.Instance.EnableAutomaticReports; }
        }

        public ReactiveUI.Legacy.ReactiveCommand SubmitCrashReport { get; private set; }
    }
}