using Espera.Core.Analytics;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    internal class CrashViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool?> sendingSucceeded;

        public CrashViewModel(Exception exception)
        {
            this.ReportContent = exception.ToString();

            this.SubmitCrashReport = this.WhenAnyValue(x => x.SendingSucceeded)
                .Select(x => x == null || !x.Value)
                .ToCommand();

            this.sendingSucceeded = this.SubmitCrashReport
                .RegisterAsyncTask(x => AnalyticsClient.Instance.RecordCrashAsync(exception, true))
                .Select(x => new bool?(x))
                .ToProperty(this, x => x.SendingSucceeded);
        }

        public string ReportContent { get; private set; }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded == null ? null : this.sendingSucceeded.Value; }
        }

        public IReactiveCommand SubmitCrashReport { get; private set; }
    }
}