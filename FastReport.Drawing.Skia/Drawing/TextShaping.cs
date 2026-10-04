using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using HbBuffer = HarfBuzzSharp.Buffer;
using HbDirection = HarfBuzzSharp.Direction;
using HbFace = HarfBuzzSharp.Face;
using HbFeature = HarfBuzzSharp.Feature;
using HbFont = HarfBuzzSharp.Font;
using HbScript = HarfBuzzSharp.Script;
using HbUnicode = HarfBuzzSharp.UnicodeFunctions;

namespace System.Drawing;

/// <summary>Glyphs of one font, in visual order, positioned relative to the start of the shaped text.</summary>
internal sealed class ShapedGlyphRun(SKFont font, int count)
{
    public SKFont Font { get; } = font;

    public ushort[] Glyphs { get; } = new ushort[count];

    /// <summary>Glyph origins including HarfBuzz offsets (mark positioning, kerning adjustments).</summary>
    public SKPoint[] Positions { get; } = new SKPoint[count];

    /// <summary>Pen position before each glyph.</summary>
    public float[] PenX { get; } = new float[count];

    public float[] Advances { get; } = new float[count];

    /// <summary>Index into the source string of the first character that produced each glyph.</summary>
    public int[] Clusters { get; } = new int[count];

    /// <summary>Start of the source text this run was shaped from.</summary>
    public int TextStart { get; init; }

    /// <summary>Length of the source text this run was shaped from.</summary>
    public int TextLength { get; init; }
}

/// <summary>The result of shaping a span of text: positioned glyph runs and per-character advances.</summary>
internal sealed class ShapedText(int length)
{
    public List<ShapedGlyphRun> Runs { get; } = [];

    public float Width { get; set; }

    /// <summary>
    /// Advance attributed to each UTF-16 unit of the span (logical order). All of a cluster's advance goes to
    /// its first character, so ligatures and combining marks never offer a break opportunity inside a cluster.
    /// </summary>
    public float[] CharAdvances { get; } = new float[length];
}

/// <summary>A HarfBuzz font for an <see cref="SKTypeface"/>, created once per typeface.</summary>
internal sealed class HarfBuzzTypeface
{
    private static readonly ConditionalWeakTable<SKTypeface, HarfBuzzTypeface> cache = new();

    private HarfBuzzTypeface(SKTypeface typeface)
    {
        // The blob takes ownership of the stream and releases it when HarfBuzz is done with the data.
        var stream = typeface.OpenStream(out int index);
        if (stream == null)
            return;

        using var blob = stream.ToHarfBuzzBlob();
        var face = new HbFace(blob, index) { Index = index, UnitsPerEm = typeface.UnitsPerEm };
        UnitsPerEm = face.UnitsPerEm > 0 ? face.UnitsPerEm : 2048;
        Face = face; // kept for font subsetting (FontSubsetScope)
        Font = new HbFont(face);
        // Shaping at the design-unit scale yields exact, unhinted advances that are scaled per font size.
        Font.SetScale(UnitsPerEm, UnitsPerEm);
        Font.SetFunctionsOpenType();
    }

    /// <summary>The HarfBuzz font, or null when the typeface data is not accessible.</summary>
    public HbFont? Font { get; }

    /// <summary>The HarfBuzz face, or null when the typeface data is not accessible.</summary>
    public HbFace? Face { get; }

    public int UnitsPerEm { get; }

    public static HarfBuzzTypeface Get(SKTypeface typeface) =>
        cache.GetValue(typeface, static t => new HarfBuzzTypeface(t));
}

/// <summary>
/// A <see cref="Font"/> resolved at a concrete em size, shaping text with HarfBuzz.
/// </summary>
/// <remarks>
/// <para>Shaping a span happens in four steps:</para>
/// <list type="number">
/// <item>Itemization: split by fallback font (the Skia equivalent of GDI+ font linking), by Unicode script,
/// and by bidirectional embedding level.</item>
/// <item>Bidi: a simplified Unicode Bidirectional Algorithm resolves levels (strong letters by script direction,
/// numbers in right-to-left context, neutrals from their neighbors) and reorders the runs visually (rule L2).</item>
/// <item>Shaping: each run is shaped by HarfBuzz with the surrounding text as context, which gives kerning,
/// ligatures, Arabic joining, Indic reordering and mark positioning.</item>
/// <item>Results are cached per span, because line breaking and trimming measure the same spans repeatedly.</item>
/// </list>
/// </remarks>
internal sealed class TextFont : IDisposable
{
    private const float Epsilon = 0.01f;

    private static readonly ConcurrentDictionary<(int CodePoint, bool Bold, bool Italic), SKTypeface?> fallbackTypefaces = new();

    private readonly Dictionary<SKTypeface, SKFont> fallbackFonts = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(string Text, int Start, int Length), ShapedText> shapeCache = new(new SpanKeyComparer());
    private readonly string familyName;
    private readonly SKFontStyle style;
    private readonly bool bold;
    private readonly bool italic;
    private bool rightToLeft;
    private float advanceScale = 1f;

    public TextFont(Font font, float emSize, SKFontEdging edging, SKFontHinting hinting)
    {
        Primary = font.CreateSKFont(emSize);
        Primary.Edging = edging;
        Primary.Hinting = hinting;
        EmSize = emSize;
        Underline = font.Underline;
        Strikeout = font.Strikeout;
        familyName = font.Name;
        bold = font.Bold;
        italic = font.Italic;
        style = new SKFontStyle(
            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        var metrics = Primary.Metrics;
        Ascent = -metrics.Ascent;
        Descent = metrics.Descent;
        LineHeight = Primary.Spacing;
        UnderlinePosition = metrics.UnderlinePosition ?? emSize * 0.1f;
        UnderlineThickness = metrics.UnderlineThickness ?? emSize / 14f;
        StrikeoutPosition = metrics.StrikeoutPosition ?? -Ascent * 0.3f;
        StrikeoutThickness = metrics.StrikeoutThickness ?? emSize / 14f;
    }

    public SKFont Primary { get; }

    public float EmSize { get; }

    public float Ascent { get; }

    public float Descent { get; }

    public float LineHeight { get; }

    public bool Underline { get; }

    public bool Strikeout { get; }

    public float UnderlinePosition { get; }

    public float UnderlineThickness { get; }

    public float StrikeoutPosition { get; }

    public float StrikeoutThickness { get; }

    /// <summary>The paragraph base direction (GDI+ <see cref="StringFormatFlags.DirectionRightToLeft"/>).</summary>
    public bool RightToLeft
    {
        get => rightToLeft;
        set
        {
            if (rightToLeft != value)
            {
                rightToLeft = value;
                shapeCache.Clear();
            }
        }
    }

    /// <summary>
    /// Factor applied to glyph advances (not to glyph shapes), spreading glyphs apart as GDI+ does for
    /// non-typographic string formats.
    /// </summary>
    public float AdvanceScale
    {
        get => advanceScale;
        set
        {
            if (advanceScale != value)
            {
                advanceScale = value;
                shapeCache.Clear();
            }
        }
    }

    public ShapedText Shape(string text, int start, int length)
    {
        var key = (text, start, length);
        if (!shapeCache.TryGetValue(key, out var shaped))
        {
            shaped = ShapeCore(text, start, length);
            shapeCache[key] = shaped;
        }
        return shaped;
    }

    public float Measure(string text, int start, int length) => Shape(text, start, length).Width;

    /// <summary>Fills per-UTF-16-unit advances (see <see cref="ShapedText.CharAdvances"/>).</summary>
    public void GetAdvances(string text, int start, int length, Span<float> advances) =>
        Shape(text, start, length).CharAdvances.CopyTo(advances);

    public float Draw(SKCanvas canvas, string text, int start, int length, float x, float y, SKPaint paint)
    {
        var shaped = Shape(text, start, length);
        if (shaped.Runs.Count > 0)
        {
            var subsetScope = FastReport.Drawing.Skia.FontSubsetScope.Current;
            List<SKFont>? substitutes = null;
            using var builder = new SKTextBlobBuilder();
            foreach (var run in shaped.Runs)
            {
                var font = run.Font;
                if (subsetScope != null)
                {
                    if (subsetScope.IsCollecting)
                    {
                        subsetScope.Record(font.Typeface, run.Glyphs, text, start, length);
                    }
                    else
                    {
                        var subset = subsetScope.Substitute(font.Typeface);
                        if (!ReferenceEquals(subset, font.Typeface))
                        {
                            font = WithTypeface(font, subset);
                            (substitutes ??= []).Add(font);
                        }
                    }
                }
                AddTextRun(builder, font, run, text);
            }
            using var blob = builder.Build();
            if (blob != null)
                canvas.DrawText(blob, x, y, paint);
            if (substitutes != null)
            {
                foreach (var font in substitutes)
                    font.Dispose();
            }
        }
        return x + shaped.Width;
    }

    /// <summary>
    /// Adds a positioned run that also carries its source text (UTF-8) and, per glyph, the byte offset of the
    /// characters it came from. Backends that extract text use this instead of guessing characters from glyphs:
    /// the PDF backend writes /ActualText for clusters whose glyphs don't map 1:1 to characters (Arabic joining
    /// forms, Indic conjuncts, ligatures), so copied text matches the original.
    /// </summary>
    private static void AddTextRun(SKTextBlobBuilder builder, SKFont font, ShapedGlyphRun run, string text)
    {
        int start = run.TextStart;
        int length = run.TextLength;
        if (length <= 0)
        {
            builder.AddPositionedRun(run.Glyphs, font, run.Positions);
            return;
        }

        // UTF-8 byte offset of each UTF-16 unit of the run's text.
        var byteOffsets = new int[length + 1];
        int bytes = 0;
        for (int i = 0; i < length; i++)
        {
            byteOffsets[i] = bytes;
            char c = text[start + i];
            if (char.IsHighSurrogate(c) && i + 1 < length && char.IsLowSurrogate(text[start + i + 1]))
            {
                byteOffsets[++i] = bytes;
                bytes += 4;
            }
            else
            {
                bytes += c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
            }
        }
        byteOffsets[length] = bytes;

        var utf8 = new byte[bytes];
        System.Text.Encoding.UTF8.GetBytes(text, start, length, utf8, 0);

        var clusters = new uint[run.Glyphs.Length];
        for (int i = 0; i < clusters.Length; i++)
        {
            int index = run.Clusters[i] - start;
            clusters[i] = (uint)byteOffsets[Math.Clamp(index, 0, length)];
        }

        var buffer = builder.AllocatePositionedTextRun(font, run.Glyphs.Length, bytes, null);
        buffer.SetGlyphs(run.Glyphs);
        buffer.SetPositions(run.Positions);
        buffer.SetText(utf8);
        buffer.SetClusters(clusters);
    }

    /// <summary>A copy of <paramref name="font"/> with another typeface (same glyph IDs, e.g. a subset).</summary>
    private static SKFont WithTypeface(SKFont font, SKTypeface typeface) => new(typeface, font.Size, font.ScaleX, font.SkewX)
    {
        Embolden = font.Embolden,
        Edging = font.Edging,
        Hinting = font.Hinting,
        Subpixel = font.Subpixel,
        LinearMetrics = font.LinearMetrics,
        BaselineSnap = font.BaselineSnap,
        EmbeddedBitmaps = font.EmbeddedBitmaps,
        ForceAutoHinting = font.ForceAutoHinting,
    };

    public float AppendToPath(SKPathBuilder path, string text, int start, int length, float x, float y)
    {
        var shaped = Shape(text, start, length);
        foreach (var run in shaped.Runs)
        {
            for (int i = 0; i < run.Glyphs.Length; i++)
            {
                using var glyph = run.Font.GetGlyphPath(run.Glyphs[i]);
                if (glyph != null)
                    path.AddPath(glyph, x + run.Positions[i].X, y + run.Positions[i].Y, SKPathAddMode.Append);
            }
        }
        return x + shaped.Width;
    }

    /// <summary>
    /// Returns the horizontal extents (relative to the span start, merged and sorted) of the glyphs produced
    /// by characters in [<paramref name="first"/>, <paramref name="last"/>). With bidi text one logical range
    /// can map to several visual pieces.
    /// </summary>
    public List<(float Left, float Right)> GetClusterExtents(string text, int start, int length, int first, int last)
    {
        var extents = new List<(float Left, float Right)>();
        foreach (var run in Shape(text, start, length).Runs)
        {
            for (int i = 0; i < run.Glyphs.Length; i++)
            {
                if (run.Clusters[i] >= first && run.Clusters[i] < last)
                    extents.Add((run.PenX[i], run.PenX[i] + run.Advances[i]));
            }
        }

        extents.Sort();
        var merged = new List<(float Left, float Right)>();
        foreach (var extent in extents)
        {
            if (merged.Count > 0 && extent.Left <= merged[^1].Right + Epsilon)
                merged[^1] = (merged[^1].Left, Math.Max(merged[^1].Right, extent.Right));
            else
                merged.Add(extent);
        }
        return merged;
    }

    public void Dispose()
    {
        Primary.Dispose();
        foreach (var font in fallbackFonts.Values)
            font.Dispose();
    }

    #region Shaping

    private ShapedText ShapeCore(string text, int start, int length)
    {
        var result = new ShapedText(length);
        if (length <= 0)
            return result;

        var items = Itemize(text, start, length);
        ReorderVisually(items);

        float penX = 0;
        foreach (var item in items)
        {
            var hb = HarfBuzzTypeface.Get(item.Font.Typeface);
            var run = hb.Font != null
                ? ShapeRun(hb, text, item, start, advanceScale, ref penX, result.CharAdvances)
                : ShapeRunUnshaped(text, item, start, advanceScale, ref penX, result.CharAdvances);
            if (run.Glyphs.Length > 0)
                result.Runs.Add(run);
        }

        result.Width = penX;
        return result;
    }

    private static ShapedGlyphRun ShapeRun(HarfBuzzTypeface hb, string text, Item item, int spanStart, float advanceScale, ref float penX, float[] charAdvances)
    {
        using var buffer = new HbBuffer();
        // Passing the whole string with an item range lets HarfBuzz use the neighbouring text as context
        // (e.g. Arabic joining across font runs), and makes clusters absolute indices into the string.
        buffer.AddUtf16(text, item.Start, item.Length);
        buffer.GuessSegmentProperties();
        buffer.Direction = item.Level % 2 == 1 ? HbDirection.RightToLeft : HbDirection.LeftToRight;
        if (IsRealScript(item.Script))
            buffer.Script = item.Script;
        hb.Font!.Shape(buffer, Array.Empty<HbFeature>());

        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        float scale = item.Font.Size / hb.UnitsPerEm;
        var run = new ShapedGlyphRun(item.Font, infos.Length) { TextStart = item.Start, TextLength = item.Length };
        for (int i = 0; i < infos.Length; i++)
        {
            float advance = positions[i].XAdvance * scale * advanceScale;
            int cluster = (int)infos[i].Cluster;
            run.Glyphs[i] = (ushort)infos[i].Codepoint;
            run.PenX[i] = penX;
            run.Advances[i] = advance;
            run.Clusters[i] = cluster;
            // HarfBuzz offsets are y-up; Skia is y-down.
            run.Positions[i] = new SKPoint(penX + positions[i].XOffset * scale, -positions[i].YOffset * scale);
            if (cluster >= spanStart && cluster - spanStart < charAdvances.Length)
                charAdvances[cluster - spanStart] += advance;
            penX += advance;
        }
        return run;
    }

    /// <summary>Fallback for typefaces whose font data cannot be read: one glyph per code point, no shaping.</summary>
    private static ShapedGlyphRun ShapeRunUnshaped(string text, Item item, int spanStart, float advanceScale, ref float penX, float[] charAdvances)
    {
        var span = text.AsSpan(item.Start, item.Length);
        var glyphs = item.Font.GetGlyphs(span);
        var widths = item.Font.GetGlyphWidths(span);
        for (int i = 0; i < widths.Length; i++)
            widths[i] *= advanceScale;
        var run = new ShapedGlyphRun(item.Font, glyphs.Length) { TextStart = item.Start, TextLength = item.Length };
        int charIndex = item.Start;
        for (int i = 0; i < glyphs.Length; i++)
        {
            run.Glyphs[i] = glyphs[i];
            run.PenX[i] = penX;
            run.Advances[i] = widths[i];
            run.Clusters[i] = charIndex;
            run.Positions[i] = new SKPoint(penX, 0);
            if (charIndex - spanStart < charAdvances.Length)
                charAdvances[charIndex - spanStart] += widths[i];
            penX += widths[i];
            charIndex += char.IsHighSurrogate(text[charIndex]) && charIndex + 1 < item.Start + item.Length ? 2 : 1;
        }
        return run;
    }

    #endregion

    #region Itemization and bidi

    private enum BidiClass
    {
        Neutral,
        Left,
        Right,
        Number,
        Mark,
    }

    private struct Item
    {
        public int Start;
        public int Length;
        public SKFont Font;
        public HbScript Script;
        public int Level;
    }

    private struct CodePointInfo
    {
        public int Index;
        public int Count;
        public SKFont Font;
        public HbScript Script;
        public BidiClass Class;
        public int Level;
    }

    private List<Item> Itemize(string text, int start, int length)
    {
        var unicode = HbUnicode.Default;
        var cps = new List<CodePointInfo>(length);
        int end = start + length;
        SKFont? currentFont = null;

        for (int i = start; i < end;)
        {
            int cp = text[i];
            int count = 1;
            if (char.IsHighSurrogate(text[i]) && i + 1 < end && char.IsLowSurrogate(text[i + 1]))
            {
                cp = char.ConvertToUtf32(text[i], text[i + 1]);
                count = 2;
            }

            currentFont = ResolveFont(cp, currentFont);
            var script = unicode.GetScript(cp);
            BidiClass cls;
            if (script == HbScript.Inherited)
                cls = BidiClass.Mark;
            else if (cp <= char.MaxValue && char.IsDigit((char)cp))
                cls = BidiClass.Number;
            else if (!IsRealScript(script))
                cls = BidiClass.Neutral;
            else
                cls = script.HorizontalDirection == HbDirection.RightToLeft ? BidiClass.Right : BidiClass.Left;

            cps.Add(new CodePointInfo { Index = i, Count = count, Font = currentFont, Script = script, Class = cls });
            i += count;
        }

        ResolveScripts(cps);
        ResolveLevels(cps);

        var items = new List<Item>();
        foreach (var cp in cps)
        {
            if (items.Count > 0)
            {
                var last = items[^1];
                if (ReferenceEquals(last.Font, cp.Font) && last.Level == cp.Level && last.Script == cp.Script)
                {
                    last.Length += cp.Count;
                    items[^1] = last;
                    continue;
                }
            }
            items.Add(new Item { Start = cp.Index, Length = cp.Count, Font = cp.Font, Script = cp.Script, Level = cp.Level });
        }
        return items;
    }

    /// <summary>Common and inherited characters (spaces, punctuation, marks) join the surrounding script run.</summary>
    private static void ResolveScripts(List<CodePointInfo> cps)
    {
        var previous = HbScript.Common;
        for (int i = 0; i < cps.Count; i++)
        {
            var cp = cps[i];
            if (IsRealScript(cp.Script))
                previous = cp.Script;
            else
            {
                cp.Script = previous;
                cps[i] = cp;
            }
        }

        // Leading common characters take the first real script that follows.
        int firstReal = cps.FindIndex(c => IsRealScript(c.Script));
        for (int i = 0; i < firstReal; i++)
        {
            var cp = cps[i];
            cp.Script = cps[firstReal].Script;
            cps[i] = cp;
        }
    }

    /// <summary>
    /// A simplified Unicode Bidirectional Algorithm (UAX #9) for a single paragraph without explicit
    /// embeddings: left-to-right letters get the even level, right-to-left letters the odd level, numbers
    /// after right-to-left text (or in a right-to-left paragraph) stay left-to-right inside it (level 2),
    /// and neutrals take the direction of their neighbours when both agree, else the paragraph direction.
    /// </summary>
    private void ResolveLevels(List<CodePointInfo> cps)
    {
        int baseLevel = rightToLeft ? 1 : 0;
        int leftLevel = baseLevel == 0 ? 0 : 2;

        // Combining marks take the class of their base character (W1).
        for (int i = 0; i < cps.Count; i++)
        {
            if (cps[i].Class != BidiClass.Mark)
                continue;
            var cp = cps[i];
            cp.Class = i > 0 ? cps[i - 1].Class : BidiClass.Neutral;
            cps[i] = cp;
        }

        // Strong characters and numbers (W7: numbers after left-to-right text behave as left-to-right).
        bool rtlContext = baseLevel == 1;
        for (int i = 0; i < cps.Count; i++)
        {
            var cp = cps[i];
            switch (cp.Class)
            {
                case BidiClass.Left:
                    cp.Level = leftLevel;
                    rtlContext = false;
                    break;
                case BidiClass.Right:
                    cp.Level = 1;
                    rtlContext = true;
                    break;
                case BidiClass.Number:
                    cp.Level = rtlContext ? 2 : leftLevel;
                    break;
                default:
                    cp.Level = -1;
                    break;
            }
            cps[i] = cp;
        }

        // Neutrals (N1/N2): between two characters of the same direction take that direction.
        for (int i = 0; i < cps.Count; i++)
        {
            if (cps[i].Level >= 0)
                continue;

            int runEnd = i;
            while (runEnd < cps.Count && cps[runEnd].Level < 0)
                runEnd++;

            bool before = i > 0 ? IsRtlLevel(cps[i - 1]) : baseLevel == 1;
            bool after = runEnd < cps.Count ? IsRtlLevel(cps[runEnd]) : baseLevel == 1;
            int level = before == after ? (before ? 1 : leftLevel) : baseLevel;

            for (int j = i; j < runEnd; j++)
            {
                var cp = cps[j];
                cp.Level = level;
                cps[j] = cp;
            }
            i = runEnd - 1;
        }

        // Numbers count as right-to-left for their neighbours (N1); letters by their own direction.
        static bool IsRtlLevel(CodePointInfo cp) => cp.Class == BidiClass.Number ? cp.Level == 2 || cp.Level == 1 : cp.Level % 2 == 1;
    }

    /// <summary>UAX #9 rule L2: from the highest level down to the lowest odd level, reverse every run at or above it.</summary>
    private static void ReorderVisually(List<Item> items)
    {
        int maxLevel = 0;
        foreach (var item in items)
            maxLevel = Math.Max(maxLevel, item.Level);

        for (int level = maxLevel; level >= 1; level--)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Level < level)
                    continue;
                int runEnd = i;
                while (runEnd < items.Count && items[runEnd].Level >= level)
                    runEnd++;
                items.Reverse(i, runEnd - i);
                i = runEnd;
            }
        }
    }

    private static bool IsRealScript(HbScript script) =>
        script != HbScript.Common && script != HbScript.Inherited && script != HbScript.Unknown;

    #endregion

    #region Font fallback

    private SKFont ResolveFont(int codePoint, SKFont? current)
    {
        // Spaces, controls and combining marks stay in the current run to avoid needless splits.
        if (codePoint < 0x20 || (codePoint <= char.MaxValue && (char.IsWhiteSpace((char)codePoint) || char.GetUnicodeCategory((char)codePoint) == Globalization.UnicodeCategory.NonSpacingMark)))
            return current ?? Primary;
        if (Primary.ContainsGlyph(codePoint))
            return Primary;
        if (current != null && current.ContainsGlyph(codePoint))
            return current;

        var typeface = fallbackTypefaces.GetOrAdd((codePoint, bold, italic), static (key, state) =>
            SKFontManager.Default.MatchCharacter(state.familyName, state.style, [], key.CodePoint),
            (familyName, style));
        if (typeface == null)
            return Primary;

        if (!fallbackFonts.TryGetValue(typeface, out var font))
        {
            font = new SKFont(typeface, Primary.Size)
            {
                Edging = Primary.Edging,
                Hinting = Primary.Hinting,
                Subpixel = Primary.Subpixel,
                LinearMetrics = Primary.LinearMetrics,
                Embolden = bold && typeface.FontWeight < 600,
                SkewX = italic && typeface.FontSlant == SKFontStyleSlant.Upright ? -0.25f : 0,
            };
            fallbackFonts[typeface] = font;
        }
        return font;
    }

    #endregion

    /// <summary>Compares span keys by string identity, avoiding hashing long strings on every lookup.</summary>
    private sealed class SpanKeyComparer : IEqualityComparer<(string Text, int Start, int Length)>
    {
        public bool Equals((string Text, int Start, int Length) x, (string Text, int Start, int Length) y) =>
            ReferenceEquals(x.Text, y.Text) && x.Start == y.Start && x.Length == y.Length;

        public int GetHashCode((string Text, int Start, int Length) key) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(key.Text), key.Start, key.Length);
    }
}
