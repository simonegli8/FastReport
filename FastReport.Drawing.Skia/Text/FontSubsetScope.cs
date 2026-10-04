using System.Drawing;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace FastReport.Drawing.Skia;

/// <summary>Size of one font before and after subsetting.</summary>
public sealed record FontSubsetInfo(string FamilyName, long OriginalSize, long SubsetSize);

/// <summary>
/// Reduces the fonts embedded by Skia's PDF backend to the glyphs that are actually used.
/// </summary>
/// <remarks>
/// <para>
/// Skia embeds the font file of every typeface used for drawing, and the SkiaSharp native build cannot subset
/// fonts itself. This scope works in two passes:
/// </para>
/// <list type="number">
/// <item>While <see cref="IsCollecting"/>, all text drawn through <see cref="Graphics"/> on this logical thread
/// records its glyphs and characters per typeface (draw the content once, e.g. onto an <see cref="SKNoDrawCanvas"/>).</item>
/// <item><see cref="CreateSubsets"/> builds a subset of each font with HarfBuzz (hb-subset). Glyph IDs are retained,
/// so the shaped text stays valid, and the character map is kept for the used characters, so text copied
/// from the PDF stays correct. Text drawn afterwards uses the subset fonts.</item>
/// </list>
/// <para>The scope applies to the current async flow; dispose it to end it.</para>
/// </remarks>
public sealed class FontSubsetScope : IDisposable
{
    private static readonly AsyncLocal<FontSubsetScope?> current = new();

    private readonly FontSubsetScope? previous;
    private readonly Dictionary<SKTypeface, Usage> usage = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<SKTypeface, SKTypeface> subsets = new(ReferenceEqualityComparer.Instance);
    private readonly List<FontSubsetInfo> statistics = [];
    private bool disposed;

    private FontSubsetScope()
    {
        previous = current.Value;
        current.Value = this;
    }

    /// <summary>The scope active on the current async flow, if any.</summary>
    public static FontSubsetScope? Current => current.Value;

    /// <summary>True until <see cref="CreateSubsets"/> has been called.</summary>
    public bool IsCollecting { get; private set; } = true;

    /// <summary>Original and subset sizes of the fonts subset so far.</summary>
    public IReadOnlyList<FontSubsetInfo> Statistics => statistics;

    /// <summary>Starts a new scope in collecting mode.</summary>
    public static FontSubsetScope Begin() => new();

    /// <summary>
    /// Creates the subset fonts from the glyphs collected so far and switches to substitution.
    /// Fonts that cannot be subset (e.g. without accessible font data) keep being used as they are.
    /// </summary>
    public void CreateSubsets()
    {
        if (!IsCollecting)
            return;
        IsCollecting = false;

        foreach (var pair in usage)
        {
            var subset = HarfBuzzSubsetter.Subset(pair.Key, pair.Value, out long originalSize, out long subsetSize);
            if (subset == null)
                continue;
            subsets[pair.Key] = subset;
            statistics.Add(new FontSubsetInfo(pair.Key.FamilyName, originalSize, subsetSize));
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        if (ReferenceEquals(current.Value, this))
            current.Value = previous;
        foreach (var subset in subsets.Values)
            subset.Dispose();
        subsets.Clear();
        usage.Clear();
    }

    /// <summary>Records glyphs drawn with <paramref name="typeface"/> and the characters they came from.</summary>
    internal void Record(SKTypeface typeface, ushort[] glyphs, string text, int start, int length)
    {
        if (!usage.TryGetValue(typeface, out var entry))
            usage[typeface] = entry = new Usage();

        foreach (ushort glyph in glyphs)
            entry.Glyphs.Add(glyph);

        int end = start + length;
        for (int i = start; i < end; i++)
        {
            int codePoint = text[i];
            if (char.IsHighSurrogate(text[i]) && i + 1 < end && char.IsLowSurrogate(text[i + 1]))
                codePoint = char.ConvertToUtf32(text[i], text[++i]);
            entry.CodePoints.Add((uint)codePoint);
        }
    }

    /// <summary>Returns the subset version of <paramref name="typeface"/>, or the typeface itself.</summary>
    internal SKTypeface Substitute(SKTypeface typeface) =>
        !IsCollecting && subsets.TryGetValue(typeface, out var subset) ? subset : typeface;

    internal sealed class Usage
    {
        public HashSet<ushort> Glyphs { get; } = [];

        public HashSet<uint> CodePoints { get; } = [];
    }
}

/// <summary>Font subsetting through HarfBuzz's hb-subset API, exported by the HarfBuzzSharp native library.</summary>
internal static class HarfBuzzSubsetter
{
    private const string Library = "libHarfBuzzSharp";

    // hb_subset_flags_t
    private const uint NoHinting = 0x00000001;   // PDF viewers don't use TrueType hinting
    private const uint RetainGids = 0x00000002;  // keep glyph IDs: the text is already shaped into them

    public static SKTypeface? Subset(SKTypeface typeface, FontSubsetScope.Usage usage, out long originalSize, out long subsetSize)
    {
        originalSize = subsetSize = 0;
        var hb = HarfBuzzTypeface.Get(typeface);
        var face = hb.Face;
        if (face == null || hb.Font == null)
            return null;

        // Glyphs reached through the used characters are pulled in by hb-subset itself. Passing them as explicit
        // glyphs too would make hb-subset keep every character that maps to them (e.g. CJK radicals such as
        // U+2F47 next to U+65E5), and PDF text extraction then reports the wrong character.
        var nominalGlyphs = new HashSet<ushort>();
        foreach (uint codePoint in usage.CodePoints)
        {
            if (hb.Font.TryGetNominalGlyph(codePoint, out uint glyph))
                nominalGlyphs.Add((ushort)glyph);
        }

        using (var stream = typeface.OpenStream(out _))
            originalSize = stream?.Length ?? 0;

        IntPtr input = hb_subset_input_create_or_fail();
        if (input == IntPtr.Zero)
            return null;
        try
        {
            IntPtr glyphSet = hb_subset_input_glyph_set(input);
            foreach (ushort glyph in usage.Glyphs)
            {
                if (!nominalGlyphs.Contains(glyph))
                    hb_set_add(glyphSet, glyph);
            }

            IntPtr unicodeSet = hb_subset_input_unicode_set(input);
            foreach (uint codePoint in usage.CodePoints)
                hb_set_add(unicodeSet, codePoint);

            hb_subset_input_set_flags(input, NoHinting | RetainGids);

            IntPtr subsetFace = hb_subset_or_fail(face.Handle, input);
            if (subsetFace == IntPtr.Zero)
                return null;
            try
            {
                IntPtr blob = hb_face_reference_blob(subsetFace);
                try
                {
                    IntPtr data = hb_blob_get_data(blob, out uint length);
                    if (data == IntPtr.Zero || length == 0)
                        return null;

                    var bytes = new byte[length];
                    Marshal.Copy(data, bytes, 0, (int)length);
                    subsetSize = length;
                    using var skData = SKData.CreateCopy(bytes);
                    return SKTypeface.FromData(skData);
                }
                finally
                {
                    hb_blob_destroy(blob);
                }
            }
            finally
            {
                hb_face_destroy(subsetFace);
            }
        }
        finally
        {
            hb_subset_input_destroy(input);
        }
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_subset_input_create_or_fail();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_subset_input_destroy(IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_subset_input_glyph_set(IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_subset_input_unicode_set(IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_subset_input_set_flags(IntPtr input, uint flags);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_set_add(IntPtr set, uint codepoint);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_subset_or_fail(IntPtr source, IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_face_reference_blob(IntPtr face);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_face_destroy(IntPtr face);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hb_blob_get_data(IntPtr blob, out uint length);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void hb_blob_destroy(IntPtr blob);
}
