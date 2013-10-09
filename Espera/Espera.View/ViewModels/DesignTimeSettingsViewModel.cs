using Akavache;
using Caliburn.Micro;
using Espera.Core.Settings;
using System;

namespace Espera.View.ViewModels
{
    internal class DesignTimeSettingsViewModel : SettingsViewModel
    {
        public DesignTimeSettingsViewModel()
            : base(DesignTime.LoadLibrary(), new ViewSettings(BlobCache.InMemory), new CoreSettings(BlobCache.InMemory, BlobCache.InMemory), new WindowManager(), Guid.NewGuid())
        { }
    }
}