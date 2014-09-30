using System.Collections.Generic;

namespace Espera.View
{
    public class ChangelogEntry
    {
        public IReadOnlyList<string> Descriptions { get; set; }

        public string Type { get; set; }
    }
}