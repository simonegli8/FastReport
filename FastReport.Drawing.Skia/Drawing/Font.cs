using SkiaSharp;

namespace System.Drawing;

/// <summary>A font face, size and style, rendered through an <see cref="SKTypeface"/>.</summary>
public sealed class Font : ICloneable, IDisposable
{
    private SKTypeface? typeface;

    public Font(string familyName, float emSize)
        : this(familyName, emSize, FontStyle.Regular, GraphicsUnit.Point, 1, false)
    {
    }

    public Font(string familyName, float emSize, FontStyle style)
        : this(familyName, emSize, style, GraphicsUnit.Point, 1, false)
    {
    }

    public Font(string familyName, float emSize, GraphicsUnit unit)
        : this(familyName, emSize, FontStyle.Regular, unit, 1, false)
    {
    }

    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
        : this(familyName, emSize, style, unit, 1, false)
    {
    }

    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
        : this(familyName, emSize, style, unit, gdiCharSet, false)
    {
    }

    /// <remarks>
    /// As in GDI+, an unknown family name does not throw: the font falls back to the generic sans-serif
    /// family and <see cref="OriginalFontName"/> keeps the requested name.
    /// </remarks>
    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont)
        : this(ResolveFamily(familyName), emSize, style, unit, gdiCharSet, gdiVerticalFont)
    {
        OriginalFontName = familyName;
    }

    public Font(FontFamily family, float emSize)
        : this(family, emSize, FontStyle.Regular, GraphicsUnit.Point, 1, false)
    {
    }

    public Font(FontFamily family, float emSize, FontStyle style)
        : this(family, emSize, style, GraphicsUnit.Point, 1, false)
    {
    }

    public Font(FontFamily family, float emSize, GraphicsUnit unit)
        : this(family, emSize, FontStyle.Regular, unit, 1, false)
    {
    }

    public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit)
        : this(family, emSize, style, unit, 1, false)
    {
    }

    public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
        : this(family, emSize, style, unit, gdiCharSet, false)
    {
    }

    public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (float.IsNaN(emSize) || float.IsInfinity(emSize) || emSize <= 0)
            throw new ArgumentException($"Value of '{emSize}' is not valid for 'emSize'.", nameof(emSize));
        if (unit == GraphicsUnit.Display)
            throw new ArgumentException("GraphicsUnit.Display is not a valid font unit.", nameof(unit));

        FontFamily = family;
        Size = emSize;
        Style = style;
        Unit = unit;
        GdiCharSet = gdiCharSet;
        GdiVerticalFont = gdiVerticalFont;
    }

    public Font(Font prototype, FontStyle newStyle)
        : this(prototype.FontFamily, prototype.Size, newStyle, prototype.Unit, prototype.GdiCharSet, prototype.GdiVerticalFont)
    {
        OriginalFontName = prototype.OriginalFontName;
    }

    public FontFamily FontFamily { get; }

    public string Name => FontFamily.Name;

    public string? OriginalFontName { get; }

    public float Size { get; }

    public FontStyle Style { get; }

    public GraphicsUnit Unit { get; }

    public byte GdiCharSet { get; }

    public bool GdiVerticalFont { get; }

    public bool IsSystemFont => false;

    public string SystemFontName => string.Empty;

    public bool Bold => (Style & FontStyle.Bold) != 0;

    public bool Italic => (Style & FontStyle.Italic) != 0;

    public bool Underline => (Style & FontStyle.Underline) != 0;

    public bool Strikeout => (Style & FontStyle.Strikeout) != 0;

    /// <summary>Size in points; pixel-based sizes are converted at 96 DPI like GDI+ does for the screen.</summary>
    public float SizeInPoints => Unit switch
    {
        GraphicsUnit.Point => Size,
        GraphicsUnit.Inch => Size * 72f,
        GraphicsUnit.Document => Size * 72f / 300f,
        GraphicsUnit.Millimeter => Size * 72f / 25.4f,
        _ => Size * 72f / 96f,
    };

    /// <summary>Line spacing in pixels at 96 DPI, rounded up.</summary>
    public int Height => (int)MathF.Ceiling(GetHeight());

    /// <summary>The Skia typeface for this font's family and style.</summary>
    public SKTypeface Typeface => typeface ??= FontFamily.GetTypeface(Style);

    public float GetHeight() => GetHeight(96f);

    public float GetHeight(float dpi)
    {
        using var font = CreateSKFont(GetSizeInPixels(dpi));
        return font.Spacing;
    }

    public float GetHeight(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        using var font = CreateSKFont(graphics.GetFontEmSize(this));
        return font.Spacing;
    }

    /// <summary>The em size in device pixels at the given resolution.</summary>
    public float GetSizeInPixels(float dpi) => Unit is GraphicsUnit.World or GraphicsUnit.Pixel
        ? Size
        : Size * SkiaConversions.PixelsPerUnit(Unit, dpi);

    /// <summary>
    /// Creates an <see cref="SKFont"/> of the given em size, synthesizing bold/italic when the typeface lacks them.
    /// The caller owns the returned font.
    /// </summary>
    public SKFont CreateSKFont(float emSize)
    {
        var face = Typeface;
        var font = new SKFont(face, emSize)
        {
            Subpixel = true,
            // Keep advances independent of hinting so that measuring and drawing agree at every scale.
            LinearMetrics = true,
        };
        if (Bold && face.FontWeight < 600)
            font.Embolden = true;
        if (Italic && face.FontSlant == SKFontStyleSlant.Upright)
            font.SkewX = -0.25f;
        return font;
    }

    public object Clone() => new Font(FontFamily, Size, Style, Unit, GdiCharSet, GdiVerticalFont);

    public void Dispose()
    {
    }

    public override bool Equals(object? obj) =>
        obj is Font other
        && other.FontFamily.Equals(FontFamily)
        && other.Size == Size
        && other.Style == Style
        && other.Unit == Unit
        && other.GdiCharSet == GdiCharSet
        && other.GdiVerticalFont == GdiVerticalFont;

    public override int GetHashCode() => HashCode.Combine(FontFamily, Size, Style, Unit);

    public override string ToString() =>
        $"[Font: Name={Name}, Size={Size}, Units={(int)Unit}, GdiCharSet={GdiCharSet}, GdiVerticalFont={GdiVerticalFont}]";

    private static FontFamily ResolveFamily(string familyName)
    {
        try
        {
            return new FontFamily(familyName);
        }
        catch (ArgumentException)
        {
            return FontFamily.GenericSansSerif;
        }
    }
}
