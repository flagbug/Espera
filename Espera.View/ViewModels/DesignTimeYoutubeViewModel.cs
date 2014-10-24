using Espera.Core.Settings;

namespace Espera.View.ViewModels
{
    internal class DesignTimeYoutubeViewModel : YoutubeViewModel
    {
        public DesignTimeYoutubeViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), DesignTime.LoadLibrary().LocalAccessControl.RegisterLocalAccessToken())
        { }
    }
}