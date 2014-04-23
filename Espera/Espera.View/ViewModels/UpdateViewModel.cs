using Akavache;
using Espera.Core;
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

        /// <summary>
        /// Used in the changelog dialog to opt-out of the automatic changelog.
        /// </summary>
        public bool DisableChangelog { get; set; }

        public IEnumerable<ChangelogReleaseEntry> ReleaseEntries
        {
            get
            {
                return BlobCache.LocalMachine.GetObjectAsync<Changelog>(BlobCacheKeys.Changelog)
                    .Select(x => x.Releases)
                    .Wait();
            }
        }

        public bool ShowChangelog
        {
            get { return this.settings.IsUpdated && this.settings.EnableChangelog; }
        }

        public void ChangelogShown()
        {
            this.settings.IsUpdated = false;

            this.settings.EnableChangelog = !this.DisableChangelog;
        }
    }
}