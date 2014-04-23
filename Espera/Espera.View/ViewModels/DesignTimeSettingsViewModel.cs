using Caliburn.Micro;
using Espera.Core.Settings;
using Espera.Services;
using System;
using System.Reactive.Linq;

namespace Espera.View.ViewModels
{
    internal class DesignTimeSettingsViewModel : SettingsViewModel
    {
        public DesignTimeSettingsViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), new WindowManager(), Guid.NewGuid(),
                new MobileApiInfo(Observable.Return(0), Observable.Return(false)))
        { }
    }
}