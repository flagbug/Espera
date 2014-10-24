using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    public class DesignTimeLocalViewModel : LocalViewModel
    {
        public DesignTimeLocalViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), DesignTime.LoadLibrary().LocalAccessControl.RegisterLocalAccessToken())
        { }
    }
}