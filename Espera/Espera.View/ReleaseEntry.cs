using System.Collections.Generic;

namespace Espera.View
{
    public class ChangelogReleaseEntry
    {
        public IReadOnlyList<ChangelogEntry> Items { get; set; }

        public string Version { get; set; }
    }
}