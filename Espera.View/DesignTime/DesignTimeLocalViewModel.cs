using Espera.Core.Settings;
using Espera.View.ViewModels;

namespace Espera.View.DesignTime
{
    public class DesignTimeLocalViewModel : LocalViewModel
    {
        public DesignTimeLocalViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), DesignTime.LoadLibrary().LocalAccessControl.RegisterLocalAccessToken())
        { }
    }
}