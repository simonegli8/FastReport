using System.Drawing.Text;
using System.Text;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// Lays text out the way GDI+ <c>DrawString</c>/<c>MeasureString</c> do: paragraph breaks, word wrapping
/// (with character wrapping for words wider than the line), tab stops, line limits, trimming, alignment
/// and the 1/6 em padding of non-typographic formats.
/// </summary>
/// <remarks>
/// Text is shaped by <see cref="TextFont"/> (HarfBuzz + simplified bidi). Each tab-separated piece of a line is
/// shaped and reordered on its own; the pieces themselves are always placed left to right.
/// </remarks>
internal sealed class TextLayout
{
    private const string Ellipsis = "…";
    private const float Epsilon = 1e-3f;

    private readonly TextFont font;
    private readonly StringFormat format;
    private readonly List<Line> lines = [];
    private readonly float maxWidth;
    private readonly float firstTabOffset;
    private readonly float[] tabStops;
    private readonly float defaultTabWidth;

    public TextLayout(string text, TextFont font, SizeF layoutSize, StringFormat? format)
    {
        this.font = font;
        this.format = format ?? new StringFormat();
        font.RightToLeft = (this.format.FormatFlags & StringFormatFlags.DirectionRightToLeft) != 0;
        Text = ProcessHotkeys(text, this.format.HotkeyPrefix);
        Padding = this.format.IsTypographic ? 0 : font.EmSize / 6f;
        tabStops = this.format.GetTabStops(out firstTabOffset);
        defaultTabWidth = font.Measure(" ", 0, 1) * 8;

        var flags = this.format.FormatFlags;
        maxWidth = layoutSize.Width > 0 ? Math.Max(layoutSize.Width - 2 * Padding, 0) : float.PositiveInfinity;
        bool wrap = (flags & StringFormatFlags.NoWrap) == 0 && !float.IsPositiveInfinity(maxWidth);

        BreakLines(wrap);
        VisibleLineCount = CountVisibleLines(layoutSize.Height, (flags & StringFormatFlags.LineLimit) != 0);
        ApplyTrimming();
    }

    private struct Line
    {
        public int Start;
        public int Length;
        public int End;
        public int ParagraphEnd;
        public float Width;
        public string? Override;
    }

    public string Text { get; }

    public float Padding { get; }

    public float LineHeight => font.LineHeight;

    public int VisibleLineCount { get; }

    /// <summary>Characters consumed by the visible lines (GDI+ <c>charactersFitted</c>).</summary>
    public int CharactersFitted => VisibleLineCount > 0 ? lines[VisibleLineCount - 1].End : 0;

    /// <summary>The measured size, including padding (GDI+ <c>MeasureString</c>).</summary>
    public SizeF Size
    {
        get
        {
            if (VisibleLineCount == 0)
                return SizeF.Empty;
            float width = 0;
            for (int i = 0; i < VisibleLineCount; i++)
                width = Math.Max(width, lines[i].Width);
            return new SizeF(width + 2 * Padding, VisibleLineCount * LineHeight);
        }
    }

    public void Draw(SKCanvas canvas, RectangleF rect, SKPaint paint)
    {
        for (int i = 0; i < VisibleLineCount; i++)
        {
            var line = lines[i];
            var (x, top) = GetLineOrigin(i, rect);
            float baseline = top + font.Ascent;
            if (line.Override != null)
                DrawSpan(canvas, line.Override, 0, line.Override.Length, x, baseline, paint);
            else
                DrawSpan(canvas, Text, line.Start, line.Length, x, baseline, paint);

            if (font.Underline && line.Width > 0)
                canvas.DrawRect(SKRect.Create(x, baseline + font.UnderlinePosition, line.Width, font.UnderlineThickness), paint);
            if (font.Strikeout && line.Width > 0)
                canvas.DrawRect(SKRect.Create(x, baseline + font.StrikeoutPosition, line.Width, font.StrikeoutThickness), paint);
        }
    }

    public void AppendToPath(SKPathBuilder path, RectangleF rect)
    {
        for (int i = 0; i < VisibleLineCount; i++)
        {
            var line = lines[i];
            var (x, top) = GetLineOrigin(i, rect);
            float baseline = top + font.Ascent;
            string text = line.Override ?? Text;
            int start = line.Override != null ? 0 : line.Start;
            int length = line.Override?.Length ?? line.Length;
            ForEachTabSegment(text, start, length, x, (s, l, segmentX) => font.AppendToPath(path, text, s, l, segmentX, baseline));

            if (font.Underline && line.Width > 0)
                path.AddRect(SKRect.Create(x, baseline + font.UnderlinePosition, line.Width, font.UnderlineThickness));
            if (font.Strikeout && line.Width > 0)
                path.AddRect(SKRect.Create(x, baseline + font.StrikeoutPosition, line.Width, font.StrikeoutThickness));
        }
    }

    /// <summary>Returns the rectangles covered by a character range on the visible lines.</summary>
    public List<RectangleF> GetRangeBounds(RectangleF rect, CharacterRange range)
    {
        var result = new List<RectangleF>();
        int rangeEnd = range.First + range.Length;
        for (int i = 0; i < VisibleLineCount; i++)
        {
            var line = lines[i];
            int lineEnd = line.Start + line.Length;
            int first = Math.Max(range.First, line.Start);
            int last = Math.Min(rangeEnd, lineEnd);
            if (first >= last)
                continue;

            var (x, top) = GetLineOrigin(i, rect);
            ForEachTabSegment(Text, line.Start, line.Length, x, (s, l, segmentX) =>
            {
                foreach (var (left, right) in font.GetClusterExtents(Text, s, l, first, last))
                    result.Add(new RectangleF(segmentX + left, top, right - left, LineHeight));
            });
        }
        return result;
    }

    private (float X, float Top) GetLineOrigin(int index, RectangleF rect)
    {
        float blockHeight = VisibleLineCount * LineHeight;
        float top = format.LineAlignment switch
        {
            StringAlignment.Center => rect.Top + (rect.Height - blockHeight) / 2,
            StringAlignment.Far => rect.Bottom - blockHeight,
            _ => rect.Top,
        } + index * LineHeight;

        var alignment = format.Alignment;
        if ((format.FormatFlags & StringFormatFlags.DirectionRightToLeft) != 0 && alignment != StringAlignment.Center)
            alignment = alignment == StringAlignment.Near ? StringAlignment.Far : StringAlignment.Near;

        float width = lines[index].Width;
        float x;
        if (rect.Width > 0)
        {
            float left = rect.Left + Padding;
            float available = rect.Width - 2 * Padding;
            x = alignment switch
            {
                StringAlignment.Center => left + (available - width) / 2,
                StringAlignment.Far => left + available - width,
                _ => left,
            };
        }
        else
        {
            // Point layout: the point is the anchor for the chosen alignment.
            x = alignment switch
            {
                StringAlignment.Center => rect.Left - width / 2,
                StringAlignment.Far => rect.Left - width - Padding,
                _ => rect.Left + Padding,
            };
        }
        return (x, top);
    }

    private void BreakLines(bool wrap)
    {
        string text = Text;
        int position = 0;
        while (position <= text.Length && text.Length > 0)
        {
            int paragraphEnd = text.IndexOfAny(['\r', '\n'], position);
            int next;
            if (paragraphEnd < 0)
            {
                paragraphEnd = text.Length;
                next = text.Length;
            }
            else
            {
                next = paragraphEnd + 1;
                if (text[paragraphEnd] == '\r' && next < text.Length && text[next] == '\n')
                    next++;
            }

            BreakParagraph(position, paragraphEnd, next, wrap);
            if (paragraphEnd == text.Length)
                break;
            position = next;
        }
    }

    private void BreakParagraph(int start, int end, int next, bool wrap)
    {
        if (start == end)
        {
            lines.Add(new Line { Start = start, Length = 0, End = next, ParagraphEnd = end, Width = 0 });
            return;
        }

        string text = Text;
        var advances = new float[end - start];
        font.GetAdvances(text, start, end - start, advances);

        int lineStart = start;
        while (lineStart < end)
        {
            float x = 0;
            int lastBreak = -1;
            int breakAt = end;
            for (int i = lineStart; i < end; i++)
            {
                char c = text[i];
                bool cjk = IsCjk(c);
                if (cjk && i > lineStart)
                    lastBreak = i;

                float advance = c == '\t' ? TabAdvance(x) : advances[i - start];
                if (wrap && !IsBreakingWhiteSpace(c) && x + advance > maxWidth + Epsilon && i > lineStart)
                {
                    if (lastBreak > lineStart)
                        breakAt = lastBreak;
                    else
                        breakAt = char.IsLowSurrogate(c) && i - 1 > lineStart ? i - 1 : i;
                    break;
                }

                x += advance;
                if (IsBreakingWhiteSpace(c) || cjk || (c == '-' && i + 1 < end && !IsBreakingWhiteSpace(text[i + 1])))
                    lastBreak = i + 1;
            }

            int lineEnd = breakAt;
            int nextStart = breakAt;
            while (nextStart < end && IsBreakingWhiteSpace(text[nextStart]))
                nextStart++;

            // Trailing spaces that hang over the edge stay on this line but are not measured.
            int contentEnd = lineEnd;
            if (lineEnd < end)
                contentEnd = nextStart;

            lines.Add(new Line
            {
                Start = lineStart,
                Length = contentEnd - lineStart,
                End = nextStart >= end ? next : nextStart,
                ParagraphEnd = end,
                Width = MeasureLine(lineStart, contentEnd),
            });
            lineStart = nextStart;
        }
    }

    private float MeasureLine(int start, int end)
    {
        if ((format.FormatFlags & StringFormatFlags.MeasureTrailingSpaces) == 0)
        {
            while (end > start && IsBreakingWhiteSpace(Text[end - 1]))
                end--;
        }
        return Advance(Text, start, end - start, 0);
    }

    private int CountVisibleLines(float layoutHeight, bool lineLimit)
    {
        if (layoutHeight <= 0 || LineHeight <= 0)
            return lines.Count;

        float ratio = layoutHeight / LineHeight;
        int count = lineLimit
            ? (int)MathF.Floor(ratio + Epsilon)
            : (int)MathF.Ceiling(ratio - Epsilon);
        return Math.Clamp(count, 0, lines.Count);
    }

    private void ApplyTrimming()
    {
        if (format.Trimming == StringTrimming.None || float.IsPositiveInfinity(maxWidth))
            return;

        for (int i = 0; i < VisibleLineCount; i++)
        {
            bool textContinues = i == VisibleLineCount - 1 && VisibleLineCount < lines.Count;
            if (textContinues || lines[i].Width > maxWidth + Epsilon)
                TrimLine(i, textContinues);
        }
    }

    private void TrimLine(int index, bool textContinues)
    {
        var line = lines[index];
        int start = line.Start;
        int end = textContinues ? line.ParagraphEnd : line.Start + line.Length;
        string text = Text;
        string? result;

        switch (format.Trimming)
        {
            case StringTrimming.Character:
                result = text.Substring(start, FitCharacters(start, end, maxWidth));
                break;
            case StringTrimming.Word:
                if (textContinues)
                    return;
                result = text.Substring(start, ToWordBoundary(start, FitCharacters(start, end, maxWidth)));
                break;
            case StringTrimming.EllipsisWord:
            {
                int count = FitCharacters(start, end, maxWidth - EllipsisWidth());
                result = text.Substring(start, ToWordBoundary(start, count)).TrimEnd() + Ellipsis;
                break;
            }
            case StringTrimming.EllipsisPath:
                result = TrimPath(start, end);
                break;
            default:
                result = text.Substring(start, FitCharacters(start, end, maxWidth - EllipsisWidth())).TrimEnd() + Ellipsis;
                break;
        }

        line.Override = result;
        line.Width = Advance(result, 0, result.Length, 0);
        lines[index] = line;
    }

    private string TrimPath(int start, int end)
    {
        string text = Text;
        int separator = text.LastIndexOfAny(['\\', '/'], end - 1, end - start);
        if (separator > start)
        {
            float suffixWidth = Advance(text, separator, end - separator, 0);
            int count = FitCharacters(start, separator, maxWidth - EllipsisWidth() - suffixWidth);
            if (count > 0)
                return text.Substring(start, count) + Ellipsis + text[separator..end];
        }
        return text.Substring(start, FitCharacters(start, end, maxWidth - EllipsisWidth())).TrimEnd() + Ellipsis;
    }

    private int FitCharacters(int start, int end, float width)
    {
        string text = Text;
        float x = 0;
        int i = start;
        while (i < end)
        {
            int count = char.IsHighSurrogate(text[i]) && i + 1 < end ? 2 : 1;
            float advance = text[i] == '\t' ? TabAdvance(x) : font.Measure(text, i, count);
            if (x + advance > width + Epsilon)
                break;
            x += advance;
            i += count;
        }
        return i - start;
    }

    private int ToWordBoundary(int start, int count)
    {
        if (start + count >= Text.Length || IsBreakingWhiteSpace(Text[start + count]))
            return count;
        for (int i = start + count - 1; i > start; i--)
        {
            if (IsBreakingWhiteSpace(Text[i]))
                return i - start;
        }
        return count;
    }

    private float EllipsisWidth() => font.Measure(Ellipsis, 0, 1);

    private float TabAdvance(float x)
    {
        if (tabStops.Length > 0)
        {
            float stop = firstTabOffset;
            foreach (float tab in tabStops)
            {
                stop += tab;
                if (stop > x + Epsilon)
                    return stop - x;
            }

            float interval = tabStops[^1];
            if (interval <= 0)
                return 0;
            while (stop <= x + Epsilon)
                stop += interval;
            return stop - x;
        }

        return defaultTabWidth <= 0 ? 0 : defaultTabWidth - x % defaultTabWidth;
    }

    /// <summary>Width of a span, honoring tab stops relative to the line start.</summary>
    private float Advance(string text, int start, int length, float x0)
    {
        float x = x0;
        ForEachTabSegment(text, start, length, x0, (s, l, segmentX) => x = segmentX + font.Measure(text, s, l));
        return x - x0;
    }

    private void DrawSpan(SKCanvas canvas, string text, int start, int length, float x, float baseline, SKPaint paint) =>
        ForEachTabSegment(text, start, length, x, (s, l, segmentX) => font.Draw(canvas, text, s, l, segmentX, baseline, paint));

    /// <summary>Calls <paramref name="action"/> for each tab-free piece with its x position.</summary>
    private void ForEachTabSegment(string text, int start, int length, float x0, Action<int, int, float> action)
    {
        float x = x0;
        int segmentStart = start;
        int end = start + length;
        for (int i = start; i <= end; i++)
        {
            if (i < end && text[i] != '\t')
                continue;

            int segmentLength = i - segmentStart;
            action(segmentStart, segmentLength, x);
            x += font.Measure(text, segmentStart, segmentLength);
            if (i < end)
                x += TabAdvance(x - x0);
            segmentStart = i + 1;
        }
    }

    private static bool IsBreakingWhiteSpace(char c) => c != ' ' && c != ' ' && char.IsWhiteSpace(c);

    private static bool IsCjk(char c) =>
        c is (>= '⺀' and <= '鿿') or (>= '가' and <= '힯') or (>= '豈' and <= '﫿') or (>= '＀' and <= '￯');

    private static string ProcessHotkeys(string text, HotkeyPrefix prefix)
    {
        if (prefix == HotkeyPrefix.None || !text.Contains('&'))
            return text;

        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '&')
            {
                if (i + 1 < text.Length && text[i + 1] == '&')
                {
                    builder.Append('&');
                    i++;
                }
                continue;
            }
            builder.Append(text[i]);
        }
        return builder.ToString();
    }
}
