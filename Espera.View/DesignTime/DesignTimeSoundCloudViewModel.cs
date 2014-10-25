using Espera.Core.Settings;
using Espera.View.ViewModels;

namespace Espera.View.DesignTime
{
    internal class DesignTimeSoundCloudViewModel : SoundCloudViewModel
    {
        public DesignTimeSoundCloudViewModel()
            : base(DesignTime.LoadLibrary(), DesignTime.LoadLibrary().LocalAccessControl.RegisterLocalAccessToken(), new CoreSettings(), new ViewSettings())
        { }
    }
}