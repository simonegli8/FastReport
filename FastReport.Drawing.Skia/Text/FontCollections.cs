using System.Runtime.InteropServices;
using SkiaSharp;

namespace System.Drawing.Text;

public enum TextRenderingHint
{
    SystemDefault = 0,
    SingleBitPerPixelGridFit = 1,
    SingleBitPerPixel = 2,
    AntiAliasGridFit = 3,
    AntiAlias = 4,
    ClearTypeGridFit = 5,
}

public enum HotkeyPrefix
{
    None = 0,
    Show = 1,
    Hide = 2,
}

public enum GenericFontFamilies
{
    Serif = 0,
    SansSerif = 1,
    Monospace = 2,
}

public abstract class FontCollection : IDisposable
{
    private protected FontCollection()
    {
    }

    public abstract FontFamily[] Families { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}

/// <summary>The font families known to the platform's Skia font manager.</summary>
public sealed class InstalledFontCollection : FontCollection
{
    public override FontFamily[] Families =>
        SKFontManager.Default.FontFamilies
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new FontFamily(name, null, trusted: true))
            .ToArray();
}

/// <summary>Fonts loaded from files or memory.</summary>
/// <remarks>
/// Unlike GDI+, families of every live private collection are also resolvable by name through
/// <see cref="FontFamily(string)"/>, which makes embedded report fonts usable on servers without installing them.
/// </remarks>
public sealed class PrivateFontCollection : FontCollection
{
    private static readonly List<WeakReference<PrivateFontCollection>> registry = [];

    private readonly List<SKTypeface> typefaces = [];

    public PrivateFontCollection()
    {
        lock (registry)
            registry.Add(new WeakReference<PrivateFontCollection>(this));
    }

    public override FontFamily[] Families
    {
        get
        {
            lock (typefaces)
            {
                return typefaces
                    .Select(t => t.FamilyName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(name => new FontFamily(name, this, trusted: true))
                    .ToArray();
            }
        }
    }

    public void AddFontFile(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        if (!File.Exists(filename))
            throw new FileNotFoundException("Font file not found.", filename);

        // Font collections (.ttc) contain several faces; FromFile returns null past the last one.
        int added = 0;
        for (int index = 0; index < 256; index++)
        {
            var typeface = SKTypeface.FromFile(filename, index);
            if (typeface == null)
                break;
            Add(typeface);
            added++;
        }

        if (added == 0)
            throw new ArgumentException($"File '{filename}' is not a supported font.", nameof(filename));
    }

    public void AddMemoryFont(IntPtr memory, int length)
    {
        var bytes = new byte[length];
        Marshal.Copy(memory, bytes, 0, length);
        AddFontData(bytes);
    }

    /// <summary>Adds a font from a managed byte array (not available in GDI+).</summary>
    public void AddFontData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var skData = SKData.CreateCopy(data);
        int added = 0;
        for (int index = 0; index < 256; index++)
        {
            var typeface = SKTypeface.FromData(skData, index);
            if (typeface == null)
                break;
            Add(typeface);
            added++;
        }

        if (added == 0)
            throw new ArgumentException("Data is not a supported font.", nameof(data));
    }

    internal bool TryGetFamilyName(string name, out string canonicalName)
    {
        lock (typefaces)
        {
            foreach (var typeface in typefaces)
            {
                if (string.Equals(typeface.FamilyName, name, StringComparison.OrdinalIgnoreCase))
                {
                    canonicalName = typeface.FamilyName;
                    return true;
                }
            }
        }

        canonicalName = name;
        return false;
    }

    internal SKTypeface? GetTypeface(string family, bool bold, bool italic)
    {
        SKTypeface? best = null;
        int bestScore = int.MaxValue;
        lock (typefaces)
        {
            foreach (var typeface in typefaces)
            {
                if (!string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                    continue;

                int targetWeight = bold ? (int)SKFontStyleWeight.Bold : (int)SKFontStyleWeight.Normal;
                bool isItalic = typeface.FontSlant != SKFontStyleSlant.Upright;
                int score = Math.Abs(typeface.FontWeight - targetWeight) + (isItalic == italic ? 0 : 1000);
                if (score < bestScore)
                {
                    best = typeface;
                    bestScore = score;
                }
            }
        }

        return best;
    }

    internal static PrivateFontCollection? Find(string name, out string canonicalName)
    {
        lock (registry)
        {
            registry.RemoveAll(r => !r.TryGetTarget(out _));
            foreach (var reference in registry)
            {
                if (reference.TryGetTarget(out var collection) && collection.TryGetFamilyName(name, out canonicalName))
                    return collection;
            }
        }

        canonicalName = name;
        return null;
    }

    protected override void Dispose(bool disposing)
    {
        // Typefaces are deliberately not disposed: Font and FontFamily instances created from this
        // collection keep referencing them, and SKTypeface releases its native handle on finalization.
        lock (registry)
            registry.RemoveAll(r => !r.TryGetTarget(out var c) || ReferenceEquals(c, this));
        base.Dispose(disposing);
    }

    private void Add(SKTypeface typeface)
    {
        lock (typefaces)
            typefaces.Add(typeface);
    }
}
