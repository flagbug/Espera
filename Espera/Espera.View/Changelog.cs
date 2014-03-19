using System.Collections.Generic;

namespace Espera.View
{
    public class Changelog
    {
        public IReadOnlyList<ChangelogReleaseEntry> Releases { get; set; }
    }
}