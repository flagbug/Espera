using Akavache;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;

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
            get { return true; }// this.settings.IsUpdated; }
        }

        public IEnumerable<ChangelogReleaseEntry> ReleaseEntries
        {
            get
            {
                return BlobCache.LocalMachine.GetObjectAsync<Changelog>("changelog")
                    .Select(x => x.Releases)
                    .Wait();
            }
        }

        public void ChangelogShown()
        {
            this.settings.IsUpdated = false;
        }
    }
}