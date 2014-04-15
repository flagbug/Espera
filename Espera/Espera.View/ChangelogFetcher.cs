using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Espera.View
{
    internal static class ChangelogFetcher
    {
        private const string ChangelogUrl = "http://getespera.com/Changelog.md";

        public static async Task<Changelog> FetchAsync()
        {
            var client = new HttpClient();

            string markdown = await client.GetStringAsync(ChangelogUrl);

            var versionMatch = new Regex("# (v.*)\r$", RegexOptions.Multiline);
            var versionMatchNoCapture = new Regex("# v.*\r$", RegexOptions.Multiline);
            var typeMatch = new Regex("## (Features|Changes|Improvements|Bugfixes)\r$", RegexOptions.Multiline);
            var typeMatchNoCapture = new Regex("## (?:Features|Changes|Improvements|Bugfixes)");

            var versions = versionMatch.Matches(markdown)
                .Cast<Match>()
                .Select(x => x.Result("$1"));

            var versionSplit = versionMatchNoCapture.Split(markdown).Where(x => x != String.Empty);

            var releases = versions.Zip(versionSplit, (version, content) =>
            {
                var types = typeMatch.Matches(content)
                    .Cast<Match>()
                    .Select(x => x.Result("$1"));
                string noNewLine = content.Replace("\r", String.Empty).Replace("\n", String.Empty);
                var changes = typeMatchNoCapture.Split(noNewLine)
                    .Where(x => x != String.Empty)
                    .Select(x => x.Split(new[] { "- " }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(y => y.Replace("  ", " ")).ToList());

                var entries = types.Zip(changes, (type, changeContent) =>
                    new ChangelogEntry
                    {
                        Type = type,
                        Descriptions = changeContent
                    }).ToList();

                return new ChangelogReleaseEntry
                {
                    Version = version,
                    Items = entries
                };
            }).ToList();

            return new Changelog
            {
                Releases = releases
            };
        }
    }
}