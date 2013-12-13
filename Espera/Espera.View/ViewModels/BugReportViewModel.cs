using Espera.Services;
using ReactiveUI;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;

namespace Espera.View.ViewModels
{
    internal class BugReportViewModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<bool?> sendingSucceeded;
        private readonly string version;
        private string message;

        public BugReportViewModel()
        {
            this.version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            this.SubmitBugReport = this.WhenAnyValue(x => x.Message, x => x.SendingSucceeded,
                (message, succeeded) => !string.IsNullOrWhiteSpace(message) && (succeeded == null || !succeeded.Value))
                .ToCommand();

            this.sendingSucceeded = this.SubmitBugReport.RegisterAsync(x =>
                FogBugzService.SubmitReport("Version " + this.version + "\n\n" + this.Message).ToObservable()
                .Select(_ => true).Catch(Observable.Return(false))).Select(x => new bool?(x))
                .ToProperty(this, x => x.SendingSucceeded);
        }

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