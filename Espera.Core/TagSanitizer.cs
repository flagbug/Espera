using System.Text;
using System.Xml;
using Rareform.Validation;

namespace Espera.Core
{
    /// <summary>
    ///     Provides a method to sanitize the tag of a song (e.g removing invalid characters).
    ///     This applies not to the song on the physical drive directly, but only to the presentation in the application.
    /// </summary>
    internal static class TagSanitizer
    {
        public static string Sanitize(string tag)
        {
            if (tag == null)
                Throw.ArgumentNullException(() => tag);

            var buffer = new StringBuilder(tag.Length);

            foreach (var c in tag) buffer.Append(XmlConvert.IsXmlChar(c) ? c : '_');

            return buffer.ToString();
        }
    }
}