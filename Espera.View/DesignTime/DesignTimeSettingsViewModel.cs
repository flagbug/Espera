using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Caliburn.Micro;
using Espera.Core.Mobile;
using Espera.Core.Settings;
using Espera.View.ViewModels;

namespace Espera.View.DesignTime
{
    internal class DesignTimeSettingsViewModel : SettingsViewModel
    {
        public DesignTimeSettingsViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(), new CoreSettings(), new WindowManager(), Guid.NewGuid(),
                new MobileApiInfo(Observable.Return(new List<MobileClient>()), Observable.Return(false)))
        { }
    }
}