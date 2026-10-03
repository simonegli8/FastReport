using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// The interior of a shape, stored as an <see cref="SKPath"/> and combined with Skia path operations
/// (so regions keep floating-point precision, unlike pixel-based <see cref="SKRegion"/>).
/// </summary>
public sealed class Region : IDisposable
{
    // GDI+ reports infinite regions with these bounds.
    private const float InfiniteOrigin = -4194304f;
    private const float InfiniteSize = 8388608f;

    /// <summary>The region shape; <c>null</c> means infinite.</summary>
    internal SKPath? path;

    public Region()
    {
    }

    public Region(RectangleF rect)
    {
        path = RectPath(rect);
    }

    public Region(Rectangle rect)
        : this((RectangleF)rect)
    {
    }

    public Region(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        this.path = new SKPath(path.path);
    }

    internal Region(SKPath? path)
    {
        this.path = path;
    }

    /// <summary>The region outline, or <c>null</c> when the region is infinite.</summary>
    public SKPath? SKPath => path;

    public Region Clone() => new(path == null ? null : new SKPath(path));

    public void MakeInfinite()
    {
        path?.Dispose();
        path = null;
    }

    public void MakeEmpty()
    {
        path?.Dispose();
        path = new SKPath();
    }

    public void Intersect(RectangleF rect) => CombineWith(RectPath(rect), CombineMode.Intersect);

    public void Intersect(Rectangle rect) => Intersect((RectangleF)rect);

    public void Intersect(GraphicsPath path) => CombineWith(new SKPath(path.path), CombineMode.Intersect);

    public void Intersect(Region region) => CombineWith(region, CombineMode.Intersect);

    public void Union(RectangleF rect) => CombineWith(RectPath(rect), CombineMode.Union);

    public void Union(Rectangle rect) => Union((RectangleF)rect);

    public void Union(GraphicsPath path) => CombineWith(new SKPath(path.path), CombineMode.Union);

    public void Union(Region region) => CombineWith(region, CombineMode.Union);

    public void Xor(RectangleF rect) => CombineWith(RectPath(rect), CombineMode.Xor);

    public void Xor(Rectangle rect) => Xor((RectangleF)rect);

    public void Xor(GraphicsPath path) => CombineWith(new SKPath(path.path), CombineMode.Xor);

    public void Xor(Region region) => CombineWith(region, CombineMode.Xor);

    public void Exclude(RectangleF rect) => CombineWith(RectPath(rect), CombineMode.Exclude);

    public void Exclude(Rectangle rect) => Exclude((RectangleF)rect);

    public void Exclude(GraphicsPath path) => CombineWith(new SKPath(path.path), CombineMode.Exclude);

    public void Exclude(Region region) => CombineWith(region, CombineMode.Exclude);

    public void Complement(RectangleF rect) => CombineWith(RectPath(rect), CombineMode.Complement);

    public void Complement(Rectangle rect) => Complement((RectangleF)rect);

    public void Complement(GraphicsPath path) => CombineWith(new SKPath(path.path), CombineMode.Complement);

    public void Complement(Region region) => CombineWith(region, CombineMode.Complement);

    public void Translate(float dx, float dy) => path?.Offset(dx, dy);

    public void Translate(int dx, int dy) => path?.Offset(dx, dy);

    public void Transform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        path?.Transform(matrix.m);
    }

    public bool IsEmpty(Graphics? g) => path != null && IsPathEmpty(path);

    public bool IsInfinite(Graphics? g) => path == null;

    public RectangleF GetBounds(Graphics? g) =>
        path == null ? new RectangleF(InfiniteOrigin, InfiniteOrigin, InfiniteSize, InfiniteSize) : path.TightBounds.ToRectangleF();

    public bool IsVisible(PointF point) => path == null || path.Contains(point.X, point.Y);

    public bool IsVisible(float x, float y) => path == null || path.Contains(x, y);

    public bool IsVisible(Point point) => IsVisible(point.X, point.Y);

    public bool IsVisible(PointF point, Graphics? g) => IsVisible(point);

    public bool IsVisible(float x, float y, Graphics? g) => IsVisible(x, y);

    public bool IsVisible(RectangleF rect)
    {
        if (path == null)
            return true;
        using var rectPath = RectPath(rect);
        using var intersection = path.Op(rectPath, SKPathOp.Intersect);
        return intersection != null && !IsPathEmpty(intersection);
    }

    public bool IsVisible(Rectangle rect) => IsVisible((RectangleF)rect);

    public bool IsVisible(RectangleF rect, Graphics? g) => IsVisible(rect);

    public bool Equals(Region region, Graphics? g)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (path == null || region.path == null)
            return path == null && region.path == null;
        using var xor = path.Op(region.path, SKPathOp.Xor);
        return xor == null || IsPathEmpty(xor);
    }

    public RectangleF[] GetRegionScans(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        using var transformed = path == null ? InfinitePath() : new SKPath(path);
        transformed.Transform(matrix.m);
        using var region = new SKRegion();
        region.SetPath(transformed);
        var rects = new List<RectangleF>();
        using var iterator = region.CreateRectIterator();
        while (iterator.Next(out var rect))
            rects.Add(RectangleF.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom));
        return rects.ToArray();
    }

    public void Dispose()
    {
        path?.Dispose();
        path = null;
    }

    internal static SKPath RectPath(RectangleF rect)
    {
        using var builder = new SKPathBuilder();
        builder.AddRect(SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height));
        return builder.Snapshot();
    }

    internal static SKPath InfinitePath() =>
        RectPath(new RectangleF(InfiniteOrigin, InfiniteOrigin, InfiniteSize, InfiniteSize));

    internal static bool IsPathEmpty(SKPath path) => path.IsEmpty || path.TightBounds.IsEmpty;

    /// <summary>
    /// Combines two shapes where <c>null</c> means infinite, returning a new path (or <c>null</c> for infinite).
    /// <paramref name="current"/> and <paramref name="other"/> are not consumed.
    /// </summary>
    internal static SKPath? Combine(SKPath? current, SKPath? other, CombineMode mode)
    {
        switch (mode)
        {
            case CombineMode.Replace:
                return other == null ? null : new SKPath(other);
            case CombineMode.Intersect:
                if (current == null)
                    return other == null ? null : new SKPath(other);
                if (other == null)
                    return new SKPath(current);
                return Op(current, other, SKPathOp.Intersect);
            case CombineMode.Union:
                if (current == null || other == null)
                    return null;
                return Op(current, other, SKPathOp.Union);
            case CombineMode.Xor:
                if (current == null && other == null)
                    return new SKPath();
                if (current == null || other == null)
                {
                    using var infinite = InfinitePath();
                    return Op(infinite, current ?? other!, SKPathOp.Difference);
                }
                return Op(current, other, SKPathOp.Xor);
            case CombineMode.Exclude:
                if (other == null)
                    return new SKPath();
                if (current == null)
                {
                    using var infinite = InfinitePath();
                    return Op(infinite, other, SKPathOp.Difference);
                }
                return Op(current, other, SKPathOp.Difference);
            case CombineMode.Complement:
                if (current == null)
                    return new SKPath();
                if (other == null)
                {
                    using var infinite = InfinitePath();
                    return Op(infinite, current, SKPathOp.Difference);
                }
                return Op(other, current, SKPathOp.Difference);
            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static SKPath Op(SKPath a, SKPath b, SKPathOp op) => a.Op(b, op) ?? new SKPath();

    private void CombineWith(Region region, CombineMode mode)
    {
        ArgumentNullException.ThrowIfNull(region);
        var result = Combine(path, region.path, mode);
        path?.Dispose();
        path = result;
    }

    private void CombineWith(SKPath shape, CombineMode mode)
    {
        using (shape)
        {
            var result = Combine(path, shape, mode);
            path?.Dispose();
            path = result;
        }
    }
}
