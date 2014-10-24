using Espera.Core.Settings;
using Espera.View.ViewModels;

namespace Espera.View.DesignTime
{
    internal class DesignTimeYoutubeViewModel : YoutubeViewModel
    {
        public DesignTimeYoutubeViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), DesignTime.LoadLibrary().LocalAccessControl.RegisterLocalAccessToken())
        { }
    }
}