using System.Collections.Concurrent;
using System.Drawing.Text;
using SkiaSharp;

namespace System.Drawing;

/// <summary>A group of typefaces with a similar design, resolved through Skia's font manager.</summary>
public sealed class FontFamily : IDisposable
{
    private static readonly ConcurrentDictionary<string, string?> systemFamilyNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<(string Family, bool Bold, bool Italic), SKTypeface> systemTypefaces = new();

    private static readonly string[] sansSerifCandidates = ["Microsoft Sans Serif", "Arial", "Liberation Sans", "DejaVu Sans", "Helvetica", "Noto Sans"];
    private static readonly string[] serifCandidates = ["Times New Roman", "Liberation Serif", "DejaVu Serif", "Times", "Noto Serif"];
    private static readonly string[] monospaceCandidates = ["Courier New", "Liberation Mono", "DejaVu Sans Mono", "Courier", "Noto Sans Mono"];

    private readonly PrivateFontCollection? privateCollection;

    public FontFamily(string name)
        : this(name, null)
    {
    }

    public FontFamily(string name, FontCollection? fontCollection)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (fontCollection is PrivateFontCollection pfc && pfc.TryGetFamilyName(name, out var privateName))
        {
            Name = privateName;
            privateCollection = pfc;
            return;
        }

        if (TryFindSystemFamily(name, out var systemName))
        {
            Name = systemName;
            return;
        }

        var registered = PrivateFontCollection.Find(name, out var registeredName);
        if (registered != null)
        {
            Name = registeredName;
            privateCollection = registered;
            return;
        }

        throw new ArgumentException($"Font '{name}' cannot be found.", nameof(name));
    }

    public FontFamily(GenericFontFamilies genericFamily)
    {
        Name = ResolveGenericName(genericFamily);
    }

    internal FontFamily(string name, PrivateFontCollection? collection, bool trusted)
    {
        Name = name;
        privateCollection = collection;
    }

    public string Name { get; }

    public static FontFamily GenericSansSerif => new(GenericFontFamilies.SansSerif);

    public static FontFamily GenericSerif => new(GenericFontFamilies.Serif);

    public static FontFamily GenericMonospace => new(GenericFontFamilies.Monospace);

    public static FontFamily[] Families => new InstalledFontCollection().Families;

    public static FontFamily[] GetFamilies(Graphics graphics) => Families;

    public string GetName(int language) => Name;

    /// <summary>
    /// Always true: styles missing from the family are synthesized (fake bold/oblique) by Skia.
    /// </summary>
    public bool IsStyleAvailable(FontStyle style) => true;

    public int GetEmHeight(FontStyle style)
    {
        int unitsPerEm = GetTypeface(style).UnitsPerEm;
        return unitsPerEm > 0 ? unitsPerEm : 2048;
    }

    public int GetCellAscent(FontStyle style) => (int)MathF.Round(-GetDesignMetrics(style).Ascent);

    public int GetCellDescent(FontStyle style) => (int)MathF.Round(GetDesignMetrics(style).Descent);

    public int GetLineSpacing(FontStyle style)
    {
        var metrics = GetDesignMetrics(style);
        return (int)MathF.Round(metrics.Descent - metrics.Ascent + metrics.Leading);
    }

    /// <summary>Returns the Skia typeface used to render this family in the given style.</summary>
    public SKTypeface GetTypeface(FontStyle style)
    {
        bool bold = (style & FontStyle.Bold) != 0;
        bool italic = (style & FontStyle.Italic) != 0;

        if (privateCollection != null)
        {
            var typeface = privateCollection.GetTypeface(Name, bold, italic);
            if (typeface != null)
                return typeface;
        }

        return systemTypefaces.GetOrAdd((Name.ToLowerInvariant(), bold, italic), static key =>
        {
            var skStyle = new SKFontStyle(
                key.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                key.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            return SKFontManager.Default.MatchFamily(key.Family, skStyle)
                ?? SKTypeface.FromFamilyName(key.Family, skStyle)
                ?? SKTypeface.Default;
        });
    }

    public void Dispose()
    {
    }

    public override bool Equals(object? obj) =>
        obj is FontFamily other
        && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
        && ReferenceEquals(privateCollection, other.privateCollection);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    public override string ToString() => $"[FontFamily: Name={Name}]";

    private SKFontMetrics GetDesignMetrics(FontStyle style)
    {
        var typeface = GetTypeface(style);
        using var font = new SKFont(typeface, GetEmHeight(style));
        return font.Metrics;
    }

    private static bool TryFindSystemFamily(string name, out string canonicalName)
    {
        var found = systemFamilyNames.GetOrAdd(name, static n =>
        {
            using var typeface = SKFontManager.Default.MatchFamily(n);
            if (typeface == null)
                return null;
            return string.Equals(typeface.FamilyName, n, StringComparison.OrdinalIgnoreCase) ? typeface.FamilyName : n;
        });

        canonicalName = found ?? name;
        return found != null;
    }

    private static string ResolveGenericName(GenericFontFamilies genericFamily)
    {
        var candidates = genericFamily switch
        {
            GenericFontFamilies.Serif => serifCandidates,
            GenericFontFamilies.Monospace => monospaceCandidates,
            _ => sansSerifCandidates,
        };

        foreach (var candidate in candidates)
        {
            if (TryFindSystemFamily(candidate, out var name))
                return name;
        }

        return SKTypeface.Default.FamilyName;
    }
}
