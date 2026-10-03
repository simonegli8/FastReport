using SkiaSharp;

namespace System.Drawing.Drawing2D;

/// <summary>A series of connected lines and curves, built with <see cref="SKPathBuilder"/>.</summary>
/// <remarks>
/// <para>
/// Follows GDI+ figure semantics: consecutive lines, arcs, Béziers and curves added to an open figure are
/// connected to it; rectangles, ellipses, polygons and pies always form their own closed figure.
/// </para>
/// <para>
/// Skia paths are immutable, so the geometry is accumulated in a builder and an <see cref="SKPath"/>
/// snapshot is taken lazily whenever the path is read, then cached until the next modification.
/// </para>
/// </remarks>
public sealed class GraphicsPath : ICloneable, IDisposable
{
    private SKPathBuilder builder;
    private SKPath? snapshot;
    private FillMode fillMode;
    private bool figureOpen;
    private SKPoint lastPoint;

    public GraphicsPath()
        : this(FillMode.Alternate)
    {
    }

    public GraphicsPath(FillMode fillMode)
    {
        builder = new SKPathBuilder();
        this.fillMode = fillMode;
    }

    public GraphicsPath(PointF[] pts, byte[] types)
        : this(pts, types, FillMode.Alternate)
    {
    }

    public GraphicsPath(PointF[] pts, byte[] types, FillMode fillMode)
        : this(fillMode)
    {
        ArgumentNullException.ThrowIfNull(pts);
        ArgumentNullException.ThrowIfNull(types);
        if (pts.Length != types.Length)
            throw new ArgumentException("Parameter is not valid.");

        for (int i = 0; i < pts.Length; i++)
        {
            switch ((PathPointType)(types[i] & (byte)PathPointType.PathTypeMask))
            {
                case PathPointType.Start:
                    builder.MoveTo(pts[i].ToSKPoint());
                    break;
                case PathPointType.Line:
                    builder.LineTo(pts[i].ToSKPoint());
                    break;
                case PathPointType.Bezier when i + 2 < pts.Length:
                    builder.CubicTo(pts[i].ToSKPoint(), pts[i + 1].ToSKPoint(), pts[i + 2].ToSKPoint());
                    i += 2;
                    break;
            }

            if ((types[i] & (byte)PathPointType.CloseSubpath) != 0)
                builder.Close();
        }
    }

    public GraphicsPath(Point[] pts, byte[] types)
        : this(SkiaConversions.ToPointFs(pts), types, FillMode.Alternate)
    {
    }

    public GraphicsPath(Point[] pts, byte[] types, FillMode fillMode)
        : this(SkiaConversions.ToPointFs(pts), types, fillMode)
    {
    }

    private GraphicsPath(SKPath source, FillMode fillMode)
    {
        builder = new SKPathBuilder(source);
        this.fillMode = fillMode;
    }

    public FillMode FillMode
    {
        get => fillMode;
        set
        {
            fillMode = value;
            snapshot = null;
        }
    }

    /// <summary>
    /// An immutable snapshot of the current geometry. It is replaced (not modified) when the path changes.
    /// </summary>
    public SKPath SKPath => path;

    /// <summary>The current geometry as an immutable Skia path (cached until the next modification).</summary>
    internal SKPath path
    {
        get
        {
            if (snapshot == null)
            {
                builder.FillType = fillMode == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd;
                snapshot = builder.Snapshot();
            }
            return snapshot;
        }
    }

    public int PointCount => PathPoints.Length;

    public PointF[] PathPoints => GetPathData().Points;

    public byte[] PathTypes => GetPathData().Types;

    public PathData PathData
    {
        get
        {
            var (points, types) = GetPathData();
            return new PathData { Points = points, Types = types };
        }
    }

    public PointF GetLastPoint() => path.LastPoint.ToPointF();

    #region Lines and curves

    public void AddLine(PointF pt1, PointF pt2) => AddLine(pt1.X, pt1.Y, pt2.X, pt2.Y);

    public void AddLine(Point pt1, Point pt2) => AddLine(pt1.X, pt1.Y, pt2.X, pt2.Y);

    public void AddLine(int x1, int y1, int x2, int y2) => AddLine((float)x1, y1, x2, y2);

    public void AddLine(float x1, float y1, float x2, float y2)
    {
        ConnectTo(x1, y1);
        Edit().LineTo(x2, y2);
        lastPoint = new SKPoint(x2, y2);
    }

    public void AddLines(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length == 0)
            return;
        ConnectTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++)
            Edit().LineTo(points[i].X, points[i].Y);
        lastPoint = points[^1].ToSKPoint();
    }

    public void AddLines(Point[] points) => AddLines(SkiaConversions.ToPointFs(points));

    public void AddArc(RectangleF rect, float startAngle, float sweepAngle) =>
        AddArc(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void AddArc(Rectangle rect, float startAngle, float sweepAngle) =>
        AddArc(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void AddArc(int x, int y, int width, int height, float startAngle, float sweepAngle) =>
        AddArc((float)x, y, width, height, startAngle, sweepAngle);

    public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        var end = AppendArc(Edit(), new SKRect(x, y, x + width, y + height), startAngle, sweepAngle, connect: figureOpen);
        if (end is SKPoint point)
        {
            lastPoint = point;
            figureOpen = true;
        }
    }

    public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4) =>
        AddBezier(pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);

    public void AddBezier(Point pt1, Point pt2, Point pt3, Point pt4) =>
        AddBezier(pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);

    public void AddBezier(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4) =>
        AddBezier((float)x1, y1, x2, y2, x3, y3, x4, y4);

    public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
        ConnectTo(x1, y1);
        Edit().CubicTo(x2, y2, x3, y3, x4, y4);
        lastPoint = new SKPoint(x4, y4);
    }

    public void AddBeziers(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 4 || (points.Length - 1) % 3 != 0)
            throw new ArgumentException("Parameter is not valid.", nameof(points));
        ConnectTo(points[0].X, points[0].Y);
        for (int i = 1; i + 2 < points.Length; i += 3)
            Edit().CubicTo(points[i].ToSKPoint(), points[i + 1].ToSKPoint(), points[i + 2].ToSKPoint());
        lastPoint = points[^1].ToSKPoint();
    }

    public void AddBeziers(params Point[] points) => AddBeziers(SkiaConversions.ToPointFs(points));

    public void AddCurve(PointF[] points) => AddCurve(points, 0.5f);

    public void AddCurve(Point[] points) => AddCurve(SkiaConversions.ToPointFs(points), 0.5f);

    public void AddCurve(Point[] points, float tension) => AddCurve(SkiaConversions.ToPointFs(points), tension);

    public void AddCurve(PointF[] points, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        AddCurve(points, 0, points.Length - 1, tension);
    }

    public void AddCurve(PointF[] points, int offset, int numberOfSegments, float tension)
    {
        lastPoint = AppendCardinalSpline(Edit(), points, offset, numberOfSegments, tension, closed: false, connect: figureOpen);
        figureOpen = true;
    }

    public void AddClosedCurve(PointF[] points) => AddClosedCurve(points, 0.5f);

    public void AddClosedCurve(Point[] points) => AddClosedCurve(SkiaConversions.ToPointFs(points), 0.5f);

    public void AddClosedCurve(Point[] points, float tension) => AddClosedCurve(SkiaConversions.ToPointFs(points), tension);

    public void AddClosedCurve(PointF[] points, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        AppendCardinalSpline(Edit(), points, 0, points.Length, tension, closed: true, connect: false);
        figureOpen = false;
    }

    #endregion

    #region Closed shapes

    public void AddRectangle(RectangleF rect)
    {
        Edit().AddRect(rect.ToSKRect());
        figureOpen = false;
    }

    public void AddRectangle(Rectangle rect) => AddRectangle((RectangleF)rect);

    public void AddRectangles(RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
            AddRectangle(rect);
    }

    public void AddRectangles(Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
            AddRectangle(rect);
    }

    public void AddEllipse(RectangleF rect) => AddEllipse(rect.X, rect.Y, rect.Width, rect.Height);

    public void AddEllipse(Rectangle rect) => AddEllipse(rect.X, rect.Y, rect.Width, rect.Height);

    public void AddEllipse(int x, int y, int width, int height) => AddEllipse((float)x, y, width, height);

    public void AddEllipse(float x, float y, float width, float height)
    {
        Edit().AddOval(new SKRect(x, y, x + width, y + height));
        figureOpen = false;
    }

    public void AddPie(Rectangle rect, float startAngle, float sweepAngle) =>
        AddPie(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void AddPie(int x, int y, int width, int height, float startAngle, float sweepAngle) =>
        AddPie((float)x, y, width, height, startAngle, sweepAngle);

    public void AddPie(float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        AppendPie(Edit(), new SKRect(x, y, x + width, y + height), startAngle, sweepAngle);
        figureOpen = false;
    }

    public void AddPolygon(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        Edit().AddPoly(SkiaConversions.ToSKPoints(points), close: true);
        figureOpen = false;
    }

    public void AddPolygon(Point[] points) => AddPolygon(SkiaConversions.ToPointFs(points));

    public void AddPath(GraphicsPath addingPath, bool connect)
    {
        ArgumentNullException.ThrowIfNull(addingPath);
        Edit().AddPath(addingPath.path, connect && figureOpen ? SKPathAddMode.Extend : SKPathAddMode.Append);
        figureOpen = addingPath.figureOpen;
        lastPoint = addingPath.lastPoint;
    }

    #endregion

    #region Text

    public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat? format) =>
        AddString(s, family, style, emSize, new RectangleF(origin, SizeF.Empty), format);

    public void AddString(string s, FontFamily family, int style, float emSize, Point origin, StringFormat? format) =>
        AddString(s, family, style, emSize, new RectangleF(origin, SizeF.Empty), format);

    public void AddString(string s, FontFamily family, int style, float emSize, Rectangle layoutRect, StringFormat? format) =>
        AddString(s, family, style, emSize, (RectangleF)layoutRect, format);

    /// <summary>Adds text outlines; <paramref name="emSize"/> is the em height in world units.</summary>
    public void AddString(string s, FontFamily family, int style, float emSize, RectangleF layoutRect, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (string.IsNullOrEmpty(s) || emSize <= 0)
            return;

        var font = new Font(family, emSize, (FontStyle)style, GraphicsUnit.World);
        using var textFont = new TextFont(font, emSize, SKFontEdging.Antialias, SKFontHinting.None);
        var layout = new TextLayout(s, textFont, layoutRect.Size, format);
        layout.AppendToPath(Edit(), layoutRect);
        figureOpen = false;
    }

    #endregion

    #region Figures

    public void StartFigure() => figureOpen = false;

    public void CloseFigure()
    {
        Edit().Close();
        figureOpen = false;
    }

    public void CloseAllFigures()
    {
        var closed = new SKPathBuilder();
        using (var iterator = path.CreateIterator(forceClose: true))
        {
            var points = new SKPoint[4];
            SKPathVerb verb;
            while ((verb = iterator.Next(points)) != SKPathVerb.Done)
            {
                switch (verb)
                {
                    case SKPathVerb.Move: closed.MoveTo(points[0]); break;
                    case SKPathVerb.Line: closed.LineTo(points[1]); break;
                    case SKPathVerb.Quad: closed.QuadTo(points[1], points[2]); break;
                    case SKPathVerb.Conic: closed.ConicTo(points[1], points[2], iterator.ConicWeight()); break;
                    case SKPathVerb.Cubic: closed.CubicTo(points[1], points[2], points[3]); break;
                    case SKPathVerb.Close: closed.Close(); break;
                }
            }
        }

        ReplaceBuilder(closed);
        figureOpen = false;
    }

    public void Reset()
    {
        Edit().Reset();
        figureOpen = false;
    }

    public void Reverse()
    {
        var reversed = new SKPathBuilder();
        reversed.ReverseAddPath(path);
        ReplaceBuilder(reversed);
    }

    #endregion

    #region Geometry

    public void Transform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        using var transformed = new SKPath(path);
        transformed.Transform(matrix.m);
        ReplaceBuilder(new SKPathBuilder(transformed));
        lastPoint = matrix.m.MapPoint(lastPoint);
    }

    public void Flatten()
    {
    }

    public void Flatten(Matrix? matrix) => Flatten(matrix, 0.25f);

    public void Flatten(Matrix? matrix, float flatness)
    {
        if (matrix != null)
            Transform(matrix);
    }

    public void Widen(Pen pen) => Widen(pen, null, 0.25f);

    public void Widen(Pen pen, Matrix? matrix) => Widen(pen, matrix, 0.25f);

    public void Widen(Pen pen, Matrix? matrix, float flatness)
    {
        ArgumentNullException.ThrowIfNull(pen);
        if (matrix != null)
            Transform(matrix);
        using var paint = new SKPaint();
        pen.ApplyTo(paint, SKMatrix.Identity);
        using var widened = paint.GetFillPath(path) ?? new SKPath();
        ReplaceBuilder(new SKPathBuilder(widened));
        fillMode = FillMode.Winding;
        figureOpen = false;
    }

    public RectangleF GetBounds() => path.TightBounds.ToRectangleF();

    public RectangleF GetBounds(Matrix? matrix) => GetBounds(matrix, null);

    public RectangleF GetBounds(Matrix? matrix, Pen? pen)
    {
        using var copy = new SKPath(path);
        if (matrix != null)
            copy.Transform(matrix.m);
        var bounds = copy.TightBounds;
        if (pen != null)
        {
            float half = Math.Max(pen.Width, 1f) / 2;
            bounds.Inflate(half, half);
        }
        return bounds.ToRectangleF();
    }

    public bool IsVisible(PointF point) => path.Contains(point.X, point.Y);

    public bool IsVisible(float x, float y) => path.Contains(x, y);

    public bool IsVisible(Point point) => path.Contains(point.X, point.Y);

    public bool IsVisible(int x, int y) => path.Contains(x, y);

    public bool IsVisible(PointF pt, Graphics? graphics) => IsVisible(pt);

    public bool IsVisible(float x, float y, Graphics? graphics) => IsVisible(x, y);

    public bool IsOutlineVisible(PointF point, Pen pen) => IsOutlineVisible(point.X, point.Y, pen);

    public bool IsOutlineVisible(float x, float y, Pen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);
        using var paint = new SKPaint();
        pen.ApplyTo(paint, SKMatrix.Identity);
        paint.StrokeWidth = Math.Max(pen.Width, 1f);
        using var outline = paint.GetFillPath(path);
        return outline != null && outline.Contains(x, y);
    }

    #endregion

    public object Clone() => new GraphicsPath(path, fillMode) { figureOpen = figureOpen, lastPoint = lastPoint };

    public void Dispose() => builder.Dispose();

    /// <summary>Returns the builder for modification, dropping the cached snapshot.</summary>
    /// <remarks>The old snapshot is not disposed: callers may still hold it, and it is immutable.</remarks>
    private SKPathBuilder Edit()
    {
        snapshot = null;
        return builder;
    }

    private void ReplaceBuilder(SKPathBuilder replacement)
    {
        builder.Dispose();
        builder = replacement;
        snapshot = null;
    }

    private void ConnectTo(float x, float y)
    {
        var point = new SKPoint(x, y);
        if (!figureOpen)
            Edit().MoveTo(point);
        else if (lastPoint != point)
            Edit().LineTo(point);
        figureOpen = true;
        lastPoint = point;
    }

    /// <summary>Reports the path as GDI+-style points and types (quadratics and conics become cubic Béziers).</summary>
    private (PointF[] Points, byte[] Types) GetPathData()
    {
        var points = new List<PointF>();
        var types = new List<byte>();

        void AddCubic(SKPoint c1, SKPoint c2, SKPoint end)
        {
            points.Add(c1.ToPointF());
            points.Add(c2.ToPointF());
            points.Add(end.ToPointF());
            types.Add((byte)PathPointType.Bezier);
            types.Add((byte)PathPointType.Bezier);
            types.Add((byte)PathPointType.Bezier);
        }

        void AddQuad(SKPoint p0, SKPoint p1, SKPoint p2) =>
            AddCubic(p0 + new SKPoint((p1.X - p0.X) * 2 / 3, (p1.Y - p0.Y) * 2 / 3),
                     p2 + new SKPoint((p1.X - p2.X) * 2 / 3, (p1.Y - p2.Y) * 2 / 3),
                     p2);

        using var iterator = path.CreateRawIterator();
        var pts = new SKPoint[4];
        SKPathVerb verb;
        while ((verb = iterator.Next(pts)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    points.Add(pts[0].ToPointF());
                    types.Add((byte)PathPointType.Start);
                    break;
                case SKPathVerb.Line:
                    points.Add(pts[1].ToPointF());
                    types.Add((byte)PathPointType.Line);
                    break;
                case SKPathVerb.Quad:
                    AddQuad(pts[0], pts[1], pts[2]);
                    break;
                case SKPathVerb.Conic:
                    var quads = SKPath.ConvertConicToQuads(pts[0], pts[1], pts[2], iterator.ConicWeight(), 2);
                    for (int i = 0; i + 2 < quads.Length; i += 2)
                        AddQuad(quads[i], quads[i + 1], quads[i + 2]);
                    break;
                case SKPathVerb.Cubic:
                    AddCubic(pts[1], pts[2], pts[3]);
                    break;
                case SKPathVerb.Close:
                    if (types.Count > 0)
                        types[^1] |= (byte)PathPointType.CloseSubpath;
                    break;
            }
        }

        return (points.ToArray(), types.ToArray());
    }

    #region Shared geometry helpers (also used by Graphics)

    /// <summary>
    /// Appends an elliptical arc using GDI+ angle semantics and returns its end point
    /// (or null if the oval is empty and nothing was added).
    /// </summary>
    /// <remarks>
    /// GDI+ measures angles geometrically (the ray from the center hits the ellipse at that angle), while
    /// Skia measures them parametrically on the unit circle before stretching. The two only agree for circles,
    /// so angles are converted with t = atan2(rx·sin θ, ry·cos θ).
    /// </remarks>
    internal static SKPoint? AppendArc(SKPathBuilder builder, SKRect oval, float startAngle, float sweepAngle, bool connect)
    {
        if (oval.Width <= 0 || oval.Height <= 0)
            return null;

        sweepAngle = Math.Clamp(sweepAngle, -360f, 360f);
        float start = ToParametric(startAngle, oval.Width / 2, oval.Height / 2);
        float end = ToParametric(startAngle + sweepAngle, oval.Width / 2, oval.Height / 2);
        float sweep = end - start;

        if (Math.Abs(sweep) >= 359.99f)
        {
            // Skia degenerates on full-circle arcTo; split it in two halves.
            float half = sweep / 2;
            builder.ArcTo(oval, start, half, !connect);
            builder.ArcTo(oval, start + half, sweep - half, false);
        }
        else
        {
            builder.ArcTo(oval, start, sweep, !connect);
        }

        double radians = end * Math.PI / 180;
        return new SKPoint(
            (float)(oval.MidX + oval.Width / 2 * Math.Cos(radians)),
            (float)(oval.MidY + oval.Height / 2 * Math.Sin(radians)));
    }

    internal static void AppendPie(SKPathBuilder builder, SKRect oval, float startAngle, float sweepAngle)
    {
        builder.MoveTo(oval.MidX, oval.MidY);
        AppendArc(builder, oval, startAngle, sweepAngle, connect: true);
        builder.Close();
    }

    /// <summary>
    /// Appends a GDI+ cardinal spline and returns its end point. Segment i runs from P[i] to P[i+1] with
    /// Bézier control points P[i] + T·(P[i+1] − P[i−1]) and P[i+1] − T·(P[i+2] − P[i]), where T = tension / 3.
    /// </summary>
    internal static SKPoint AppendCardinalSpline(SKPathBuilder builder, PointF[] points, int offset, int numberOfSegments, float tension, bool closed, bool connect)
    {
        ArgumentNullException.ThrowIfNull(points);
        int n = points.Length;
        if (n < 2 || numberOfSegments < 1 || offset < 0 || offset + (closed ? 0 : numberOfSegments) > n - (closed ? 0 : 1))
            throw new ArgumentException("Parameter is not valid.", nameof(points));

        float t = tension / 3f;
        PointF At(int i) => closed ? points[((i % n) + n) % n] : points[Math.Clamp(i, 0, n - 1)];

        var first = At(offset);
        if (connect)
            builder.LineTo(first.X, first.Y);
        else
            builder.MoveTo(first.X, first.Y);

        for (int i = offset; i < offset + numberOfSegments; i++)
        {
            PointF p0 = At(i - 1), p1 = At(i), p2 = At(i + 1), p3 = At(i + 2);
            builder.CubicTo(
                p1.X + t * (p2.X - p0.X), p1.Y + t * (p2.Y - p0.Y),
                p2.X - t * (p3.X - p1.X), p2.Y - t * (p3.Y - p1.Y),
                p2.X, p2.Y);
        }

        if (closed)
            builder.Close();
        return At(offset + numberOfSegments).ToSKPoint();
    }

    private static float ToParametric(float angle, float rx, float ry)
    {
        if (rx == ry)
            return angle;
        double radians = angle * Math.PI / 180;
        double t = Math.Atan2(rx * Math.Sin(radians), ry * Math.Cos(radians)) * 180 / Math.PI;
        // Keep the same number of full turns as the input angle.
        return (float)(t + 360 * Math.Round((angle - t) / 360));
    }

    #endregion
}

public sealed class PathData
{
    public PointF[]? Points { get; set; }

    public byte[]? Types { get; set; }
}
