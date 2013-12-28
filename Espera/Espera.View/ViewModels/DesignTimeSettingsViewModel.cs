using Caliburn.Micro;
using Espera.Core.Settings;
using System;

namespace Espera.View.ViewModels
{
    internal class DesignTimeSettingsViewModel : SettingsViewModel
    {
        public DesignTimeSettingsViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), new WindowManager(), Guid.NewGuid())
        { }
    }
}