using SkiaSharp;

namespace System.Drawing.Drawing2D;

/// <summary>
/// A 3x2 affine transform with GDI+ semantics, backed by <see cref="SKMatrix"/>.
/// </summary>
/// <remarks>
/// GDI+ uses row vectors (<c>p' = p * M</c>) while Skia uses column vectors (<c>p' = M * p</c>).
/// The element layout maps as m11 = ScaleX, m12 = SkewY, m21 = SkewX, m22 = ScaleY, dx = TransX, dy = TransY,
/// and "prepend" in GDI+ (apply the new operation first) corresponds to <c>Concat(this, other)</c> in Skia.
/// </remarks>
public sealed class Matrix : ICloneable, IDisposable
{
    internal SKMatrix m;

    public Matrix()
    {
        m = SKMatrix.Identity;
    }

    public Matrix(float m11, float m12, float m21, float m22, float dx, float dy)
    {
        m = new SKMatrix(m11, m21, dx, m12, m22, dy, 0, 0, 1);
    }

    public Matrix(System.Numerics.Matrix3x2 matrix)
        : this(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32)
    {
    }

    /// <summary>
    /// Creates a matrix that maps <paramref name="rect"/> to the parallelogram defined by
    /// three points: upper-left, upper-right and lower-left.
    /// </summary>
    public Matrix(RectangleF rect, PointF[] plgpts)
    {
        ArgumentNullException.ThrowIfNull(plgpts);
        if (plgpts.Length != 3)
            throw new ArgumentException("Parameter is not valid.", nameof(plgpts));
        m = MapRectToParallelogram(rect, plgpts[0], plgpts[1], plgpts[2]);
    }

    public Matrix(Rectangle rect, Point[] plgpts)
        : this((RectangleF)rect, plgpts is null ? null! : Array.ConvertAll(plgpts, p => (PointF)p))
    {
    }

    internal Matrix(SKMatrix matrix)
    {
        m = matrix;
    }

    public float[] Elements => [m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY];

    public System.Numerics.Matrix3x2 MatrixElements
    {
        get => new(m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY);
        set => m = new SKMatrix(value.M11, value.M21, value.M31, value.M12, value.M22, value.M32, 0, 0, 1);
    }

    public float OffsetX => m.TransX;

    public float OffsetY => m.TransY;

    public bool IsIdentity => m.IsIdentity;

    public bool IsInvertible => m.IsInvertible;

    /// <summary>The underlying Skia matrix.</summary>
    public SKMatrix SKMatrix => m;

    public void Reset() => m = SKMatrix.Identity;

    public void Multiply(Matrix matrix) => Multiply(matrix, MatrixOrder.Prepend);

    public void Multiply(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        m = Combine(m, matrix.m, order);
    }

    public void Translate(float offsetX, float offsetY) => Translate(offsetX, offsetY, MatrixOrder.Prepend);

    public void Translate(float offsetX, float offsetY, MatrixOrder order) =>
        m = Combine(m, SKMatrix.CreateTranslation(offsetX, offsetY), order);

    public void Scale(float scaleX, float scaleY) => Scale(scaleX, scaleY, MatrixOrder.Prepend);

    public void Scale(float scaleX, float scaleY, MatrixOrder order) =>
        m = Combine(m, SKMatrix.CreateScale(scaleX, scaleY), order);

    public void Rotate(float angle) => Rotate(angle, MatrixOrder.Prepend);

    public void Rotate(float angle, MatrixOrder order) =>
        m = Combine(m, SKMatrix.CreateRotationDegrees(angle), order);

    public void RotateAt(float angle, PointF point) => RotateAt(angle, point, MatrixOrder.Prepend);

    public void RotateAt(float angle, PointF point, MatrixOrder order) =>
        m = Combine(m, SKMatrix.CreateRotationDegrees(angle, point.X, point.Y), order);

    public void Shear(float shearX, float shearY) => Shear(shearX, shearY, MatrixOrder.Prepend);

    public void Shear(float shearX, float shearY, MatrixOrder order) =>
        m = Combine(m, SKMatrix.CreateSkew(shearX, shearY), order);

    public void Invert()
    {
        if (!m.TryInvert(out var inverse))
            throw new ArgumentException("Matrix is not invertible.");
        m = inverse;
    }

    public void TransformPoints(PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (int i = 0; i < pts.Length; i++)
        {
            var p = m.MapPoint(pts[i].X, pts[i].Y);
            pts[i] = new PointF(p.X, p.Y);
        }
    }

    public void TransformPoints(Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (int i = 0; i < pts.Length; i++)
        {
            var p = m.MapPoint(pts[i].X, pts[i].Y);
            pts[i] = new Point((int)MathF.Round(p.X), (int)MathF.Round(p.Y));
        }
    }

    public void TransformVectors(PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (int i = 0; i < pts.Length; i++)
        {
            var p = m.MapVector(pts[i].X, pts[i].Y);
            pts[i] = new PointF(p.X, p.Y);
        }
    }

    public void TransformVectors(Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (int i = 0; i < pts.Length; i++)
        {
            var p = m.MapVector(pts[i].X, pts[i].Y);
            pts[i] = new Point((int)MathF.Round(p.X), (int)MathF.Round(p.Y));
        }
    }

    public void VectorTransformPoints(Point[] pts) => TransformVectors(pts);

    public Matrix Clone() => new(m);

    object ICloneable.Clone() => Clone();

    public void Dispose()
    {
    }

    public override bool Equals(object? obj) => obj is Matrix other && other.m == m;

    public override int GetHashCode() => m.GetHashCode();

    internal static SKMatrix Combine(SKMatrix current, SKMatrix other, MatrixOrder order) =>
        order == MatrixOrder.Prepend ? SKMatrix.Concat(current, other) : SKMatrix.Concat(other, current);

    internal static SKMatrix MapRectToParallelogram(RectangleF rect, PointF p0, PointF p1, PointF p2)
    {
        float w = rect.Width == 0 ? 1 : rect.Width;
        float h = rect.Height == 0 ? 1 : rect.Height;
        float m11 = (p1.X - p0.X) / w;
        float m12 = (p1.Y - p0.Y) / w;
        float m21 = (p2.X - p0.X) / h;
        float m22 = (p2.Y - p0.Y) / h;
        float dx = p0.X - m11 * rect.X - m21 * rect.Y;
        float dy = p0.Y - m12 * rect.X - m22 * rect.Y;
        return new SKMatrix(m11, m21, dx, m12, m22, dy, 0, 0, 1);
    }
}
