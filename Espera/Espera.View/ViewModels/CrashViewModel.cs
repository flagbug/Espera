using Espera.Services;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;

namespace Espera.View.ViewModels
{
    internal class CrashViewModel : ReactiveObject
    {
        private readonly Exception exception;
        private readonly ObservableAsPropertyHelper<bool?> sendingSucceeded;
        private readonly string version;

        public CrashViewModel(Exception exception)
        {
            this.exception = exception;
            this.version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            this.SubmitCrashReport = this.WhenAnyValue(x => x.SendingSucceeded)
                .Select(x => x == null || !x.Value)
                .ToCommand();

            this.sendingSucceeded = this.SubmitCrashReport.RegisterAsync(x =>
                FogBugzService.SubmitReport(this.ReportContent).ToObservable().Select(_ => true).Catch(Observable.Return(false)))
                .Select(x => new bool?(x))
                .ToProperty(this, x => x.SendingSucceeded);
        }

        public string ReportContent
        {
            get { return "Version " + this.version + "\n\n" + this.exception; }
        }

        public bool? SendingSucceeded
        {
            get { return this.sendingSucceeded == null ? null : this.sendingSucceeded.Value; }
        }

        public IReactiveCommand SubmitCrashReport { get; private set; }
    }
}