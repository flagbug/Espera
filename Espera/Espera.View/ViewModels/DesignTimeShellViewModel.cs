using Caliburn.Micro;
using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    internal class DesignTimeShellViewModel : ShellViewModel
    {
        public DesignTimeShellViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), new WindowManager())
        { }
    }
}