using Caliburn.Micro;
using Espera.Core.Settings;
using Espera.Services;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    internal class DesignTimeShellViewModel : ShellViewModel
    {
        public DesignTimeShellViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), new WindowManager(), new MobileApiInfo(Observable.Return(0)))
        { }
    }
}