using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// Conversions between the in-box System.Drawing primitives (System.Drawing.Primitives) and SkiaSharp types.
/// </summary>
public static class SkiaConversions
{
    public static SKPoint ToSKPoint(this PointF point) => new(point.X, point.Y);

    public static SKPoint ToSKPoint(this Point point) => new(point.X, point.Y);

    public static SKSize ToSKSize(this SizeF size) => new(size.Width, size.Height);

    public static SKRect ToSKRect(this RectangleF rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    public static SKRect ToSKRect(this Rectangle rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    public static SKRectI ToSKRectI(this Rectangle rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    public static SKColor ToSKColor(this Color color) => new(color.R, color.G, color.B, color.A);

    public static PointF ToPointF(this SKPoint point) => new(point.X, point.Y);

    public static SizeF ToSizeF(this SKSize size) => new(size.Width, size.Height);

    public static RectangleF ToRectangleF(this SKRect rect) => RectangleF.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);

    public static Rectangle ToRectangle(this SKRectI rect) => Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);

    public static Color ToColor(this SKColor color) => Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);

    internal static SKPoint[] ToSKPoints(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var result = new SKPoint[points.Length];
        for (int i = 0; i < points.Length; i++)
            result[i] = new SKPoint(points[i].X, points[i].Y);
        return result;
    }

    internal static PointF[] ToPointFs(Point[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        return Array.ConvertAll(points, p => (PointF)p);
    }

    internal static SKColor Lerp(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)MathF.Round(from.R + (to.R - from.R) * t),
            (byte)MathF.Round(from.G + (to.G - from.G) * t),
            (byte)MathF.Round(from.B + (to.B - from.B) * t),
            (byte)MathF.Round(from.A + (to.A - from.A) * t));
    }

    /// <summary>Number of device pixels per one <paramref name="unit"/> at the given resolution.</summary>
    internal static float PixelsPerUnit(GraphicsUnit unit, float dpi) => unit switch
    {
        GraphicsUnit.Point => dpi / 72f,
        GraphicsUnit.Inch => dpi,
        GraphicsUnit.Document => dpi / 300f,
        GraphicsUnit.Millimeter => dpi / 25.4f,
        _ => 1f,
    };
}
