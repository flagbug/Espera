using Akavache;
using Espera.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Splat;

namespace Espera.View.ViewModels
{
    public class UpdateViewModel : IEnableLogger
    {
        private readonly ViewSettings settings;

        public UpdateViewModel(ViewSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            this.settings = settings;

            this.OpenPortableDownloadLink = ReactiveCommand.CreateAsyncTask(_ => Task.Run(() =>
            {
                try
                {
                    Process.Start(this.PortableDownloadLink);
                }

                catch (Win32Exception ex)
                {
                    this.Log().ErrorException("Could not open link \{this.PortableDownloadLink}", ex);
                }
            }));
        }

        /// <summary>
        /// Used in the changelog dialog to opt-out of the automatic changelog.
        /// </summary>
        public bool DisableChangelog { get; set; }

        public ReactiveCommand<Unit> OpenPortableDownloadLink { get; }

        public string PortableDownloadLink => "http://getespera.com/EsperaPortable.zip";

        public IEnumerable<ChangelogReleaseEntry> ReleaseEntries
        {
            get
            {
                return BlobCache.LocalMachine.GetObject<Changelog>(BlobCacheKeys.Changelog)
                    .Select(x => x.Releases)
                    .Wait();
            }
        }

        public bool ShowChangelog => this.settings.IsUpdated && this.settings.EnableChangelog;

        public void ChangelogShown()
        {
            this.settings.IsUpdated = false;

            this.settings.EnableChangelog = !this.DisableChangelog;
        }
    }
}