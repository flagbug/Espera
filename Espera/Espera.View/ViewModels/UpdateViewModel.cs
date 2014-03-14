using System;

namespace Espera.View.ViewModels
{
    public class UpdateViewModel
    {
        private readonly ViewSettings settings;

        public UpdateViewModel(ViewSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.settings = settings;
        }

        public bool IsUpdated
        {
            get { return this.settings.IsUpdated; }
        }

        public void ChangelogShown()
        {
            this.settings.IsUpdated = false;
        }
    }
}