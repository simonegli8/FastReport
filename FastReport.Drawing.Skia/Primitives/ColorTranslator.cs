using System.Globalization;

namespace System.Drawing;

/// <summary>Translates colors to and from HTML, OLE and Win32 representations.</summary>
public static class ColorTranslator
{
    private const int Win32RedShift = 0;
    private const int Win32GreenShift = 8;
    private const int Win32BlueShift = 16;

    public static int ToWin32(Color c) => c.R << Win32RedShift | c.G << Win32GreenShift | c.B << Win32BlueShift;

    public static int ToOle(Color c) => ToWin32(c);

    public static Color FromWin32(int win32Color) => FromOle(win32Color);

    public static Color FromOle(int oleColor) =>
        Color.FromArgb((byte)(oleColor >> Win32RedShift), (byte)(oleColor >> Win32GreenShift), (byte)(oleColor >> Win32BlueShift));

    /// <summary>Parses "#RGB", "#RRGGBB", color names (including "LightGrey") and CSS system color names.</summary>
    public static Color FromHtml(string htmlColor)
    {
        if (string.IsNullOrEmpty(htmlColor))
            return Color.Empty;

        if (htmlColor[0] == '#' && (htmlColor.Length == 7 || htmlColor.Length == 4))
        {
            if (htmlColor.Length == 7)
            {
                return Color.FromArgb(
                    int.Parse(htmlColor.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(htmlColor.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(htmlColor.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }

            int r = int.Parse(htmlColor.Substring(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int g = int.Parse(htmlColor.Substring(2, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int b = int.Parse(htmlColor.Substring(3, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return Color.FromArgb(r * 17, g * 17, b * 17);
        }

        if (string.Equals(htmlColor, "LightGrey", StringComparison.OrdinalIgnoreCase))
            return Color.LightGray;

        foreach (var pair in SystemColorHtmlNames)
        {
            if (string.Equals(htmlColor, pair.Html, StringComparison.OrdinalIgnoreCase))
                return Color.FromKnownColor(pair.Color);
        }

        var named = Color.FromName(htmlColor);
        if (named.IsKnownColor)
            return named;

        throw new ArgumentException($"{htmlColor} is not a valid value for Int32.", nameof(htmlColor));
    }

    public static string ToHtml(Color c)
    {
        if (c.IsEmpty)
            return string.Empty;

        if (c.IsSystemColor)
        {
            foreach (var pair in SystemColorHtmlNames)
            {
                if (pair.Color == c.ToKnownColor())
                    return pair.Html;
            }
        }
        else if (c.IsNamedColor)
        {
            return c == Color.LightGray ? "LightGrey" : c.Name;
        }

        return "#" + c.R.ToString("X2", null) + c.G.ToString("X2", null) + c.B.ToString("X2", null);
    }

    private static readonly (string Html, KnownColor Color)[] SystemColorHtmlNames =
    [
        ("activeborder", KnownColor.ActiveBorder),
        ("activecaption", KnownColor.ActiveCaption),
        ("appworkspace", KnownColor.AppWorkspace),
        ("background", KnownColor.Desktop),
        ("buttonface", KnownColor.Control),
        ("buttonhighlight", KnownColor.ControlLightLight),
        ("buttonshadow", KnownColor.ControlDark),
        ("buttontext", KnownColor.ControlText),
        ("captiontext", KnownColor.ActiveCaptionText),
        ("graytext", KnownColor.GrayText),
        ("highlight", KnownColor.Highlight),
        ("highlighttext", KnownColor.HighlightText),
        ("inactiveborder", KnownColor.InactiveBorder),
        ("inactivecaption", KnownColor.InactiveCaption),
        ("inactivecaptiontext", KnownColor.InactiveCaptionText),
        ("infobackground", KnownColor.Info),
        ("infotext", KnownColor.InfoText),
        ("menu", KnownColor.Menu),
        ("menutext", KnownColor.MenuText),
        ("scrollbar", KnownColor.ScrollBar),
        ("threeddarkshadow", KnownColor.ControlDarkDark),
        ("threedface", KnownColor.Control),
        ("threedhighlight", KnownColor.ControlLight),
        ("threedlightshadow", KnownColor.ControlLight),
        ("window", KnownColor.Window),
        ("windowframe", KnownColor.WindowFrame),
        ("windowtext", KnownColor.WindowText),
    ];
}
