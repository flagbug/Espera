using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    internal class DesignTimeSoundCloudViewModel : SoundCloudViewModel
    {
        public DesignTimeSoundCloudViewModel()
            : base(DesignTime.LoadLibrary(), DesignTime.LoadLibrary().LocalAccessControl.RegisterLocalAccessToken(), new CoreSettings(), new ViewSettings())
        { }
    }
}