using System.Globalization;
using System.Text.RegularExpressions;

namespace FastReport.WebForms
{
    /// <summary>Turns a standalone SVG document into markup that can be embedded in an HTML page.</summary>
    internal static class SvgInliner
    {
        private static readonly Regex XmlDeclaration = new Regex(@"^\s*<\?xml[^>]*\?>\s*", RegexOptions.Compiled);
        private static readonly Regex Ids = new Regex(@"\bid=""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex UrlReferences = new Regex(@"url\(#([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex HrefReferences = new Regex(@"\b((?:xlink:)?href)=""#([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex RootSize = new Regex(@"^<svg\b([^>]*?)\swidth=""([\d.]+)""\s+height=""([\d.]+)""", RegexOptions.Compiled);

        /// <summary>
        /// Removes the XML declaration, prefixes every id (and reference to it) so that several SVGs can share one
        /// HTML document without clashing, and adds a viewBox so the SVG scales with CSS.
        /// </summary>
        public static string Inline(string svg, string idPrefix, string cssClass)
        {
            svg = XmlDeclaration.Replace(svg, string.Empty);
            svg = Ids.Replace(svg, m => $"id=\"{idPrefix}{m.Groups[1].Value}\"");
            svg = UrlReferences.Replace(svg, m => $"url(#{idPrefix}{m.Groups[1].Value})");
            svg = HrefReferences.Replace(svg, m => $"{m.Groups[1].Value}=\"#{idPrefix}{m.Groups[2].Value}\"");
            svg = RootSize.Replace(svg, m =>
            {
                string width = m.Groups[2].Value, height = m.Groups[3].Value;
                return string.Format(CultureInfo.InvariantCulture,
                    "<svg{0} class=\"{1}\" width=\"{2}\" height=\"{3}\" viewBox=\"0 0 {2} {3}\"",
                    m.Groups[1].Value, cssClass, width, height);
            }, 1);
            return svg;
        }
    }
}
