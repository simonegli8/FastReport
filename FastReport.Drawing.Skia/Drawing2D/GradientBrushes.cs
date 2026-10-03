using SkiaSharp;

namespace System.Drawing.Drawing2D;

/// <summary>A two-color (or multicolor) linear gradient.</summary>
public sealed class LinearGradientBrush : Brush
{
    private Color[] linearColors;
    private SKPoint start;
    private SKPoint end;
    private RectangleF rectangle;
    private Blend? blend;
    private ColorBlend? interpolationColors;

    public LinearGradientBrush(PointF point1, PointF point2, Color color1, Color color2)
    {
        linearColors = [color1, color2];
        start = point1.ToSKPoint();
        end = point2.ToSKPoint();
        rectangle = RectangleF.FromLTRB(
            Math.Min(point1.X, point2.X), Math.Min(point1.Y, point2.Y),
            Math.Max(point1.X, point2.X), Math.Max(point1.Y, point2.Y));
    }

    public LinearGradientBrush(Point point1, Point point2, Color color1, Color color2)
        : this((PointF)point1, (PointF)point2, color1, color2)
    {
    }

    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
        : this(rect, color1, color2, ModeToAngle(linearGradientMode), linearGradientMode is LinearGradientMode.ForwardDiagonal or LinearGradientMode.BackwardDiagonal)
    {
    }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
        : this((RectangleF)rect, color1, color2, linearGradientMode)
    {
    }

    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle)
        : this(rect, color1, color2, angle, false)
    {
    }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle)
        : this((RectangleF)rect, color1, color2, angle, false)
    {
    }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle, bool isAngleScaleable)
        : this((RectangleF)rect, color1, color2, angle, isAngleScaleable)
    {
    }

    /// <summary>
    /// The gradient runs along <paramref name="angle"/> so that color1 sits on the rectangle corner with the
    /// smallest projection onto that direction and color2 on the corner with the largest, matching GDI+.
    /// </summary>
    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle, bool isAngleScaleable)
    {
        if (rect.Width == 0 || rect.Height == 0)
            throw new ArgumentException($"Rectangle '{rect}' cannot have a width or height equal to 0.");

        linearColors = [color1, color2];
        rectangle = rect;

        double radians = angle * Math.PI / 180;
        double dx = Math.Cos(radians);
        double dy = Math.Sin(radians);
        if (isAngleScaleable)
        {
            // The angle is defined in a unit square stretched to the rectangle: the isolines follow the
            // stretch, so the gradient normal is scaled by the inverse of the rectangle's size.
            dx /= rect.Width;
            dy /= rect.Height;
            double length = Math.Sqrt(dx * dx + dy * dy);
            dx /= length;
            dy /= length;
        }

        double cx = rect.X + rect.Width / 2.0;
        double cy = rect.Y + rect.Height / 2.0;
        double halfExtent = Math.Abs(rect.Width / 2.0 * dx) + Math.Abs(rect.Height / 2.0 * dy);
        start = new SKPoint((float)(cx - dx * halfExtent), (float)(cy - dy * halfExtent));
        end = new SKPoint((float)(cx + dx * halfExtent), (float)(cy + dy * halfExtent));
    }

    public Color[] LinearColors
    {
        get => (Color[])linearColors.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length < 2)
                throw new ArgumentException("Two colors are required.", nameof(value));
            linearColors = [value[0], value[1]];
        }
    }

    public RectangleF Rectangle => rectangle;

    public WrapMode WrapMode { get; set; } = WrapMode.Tile;

    public bool GammaCorrection { get; set; }

    public Blend? Blend
    {
        get => blend;
        set
        {
            blend = value;
            interpolationColors = null;
        }
    }

    public ColorBlend? InterpolationColors
    {
        get => interpolationColors;
        set
        {
            interpolationColors = value;
            blend = null;
        }
    }

    public Matrix Transform
    {
        get => transform.Clone();
        set => transform = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
    }

    public void ResetTransform() => transform.Reset();

    public void MultiplyTransform(Matrix matrix) => MultiplyTransformCore(matrix, MatrixOrder.Prepend);

    public void MultiplyTransform(Matrix matrix, MatrixOrder order) => MultiplyTransformCore(matrix, order);

    public void TranslateTransform(float dx, float dy, MatrixOrder order = MatrixOrder.Prepend) => transform.Translate(dx, dy, order);

    public void ScaleTransform(float sx, float sy, MatrixOrder order = MatrixOrder.Prepend) => transform.Scale(sx, sy, order);

    public void RotateTransform(float angle, MatrixOrder order = MatrixOrder.Prepend) => transform.Rotate(angle, order);

    public void SetBlendTriangularShape(float focus) => SetBlendTriangularShape(focus, 1f);

    public void SetBlendTriangularShape(float focus, float scale)
    {
        ValidateFocusScale(focus, scale);
        if (focus == 0)
            Blend = new Blend(2) { Positions = [0, 1], Factors = [scale, 0] };
        else if (focus == 1)
            Blend = new Blend(2) { Positions = [0, 1], Factors = [0, scale] };
        else
            Blend = new Blend(3) { Positions = [0, focus, 1], Factors = [0, scale, 0] };
    }

    public void SetSigmaBellShape(float focus) => SetSigmaBellShape(focus, 1f);

    /// <summary>
    /// Approximates the GDI+ bell curve: a normal-CDF ramp from 0 to <paramref name="scale"/> on [0, focus]
    /// and back down on [focus, 1].
    /// </summary>
    public void SetSigmaBellShape(float focus, float scale)
    {
        ValidateFocusScale(focus, scale);
        const int samples = 16;
        var positions = new List<float>();
        var factors = new List<float>();

        if (focus > 0)
        {
            for (int i = 0; i <= samples; i++)
            {
                float u = i / (float)samples;
                positions.Add(u * focus);
                factors.Add(scale * NormalRamp(u));
            }
        }

        if (focus < 1)
        {
            for (int i = focus > 0 ? 1 : 0; i <= samples; i++)
            {
                float u = i / (float)samples;
                positions.Add(focus + u * (1 - focus));
                factors.Add(scale * NormalRamp(1 - u));
            }
        }

        Blend = new Blend(positions.Count) { Positions = positions.ToArray(), Factors = factors.ToArray() };
    }

    public override object Clone()
    {
        var clone = (LinearGradientBrush)MemberwiseClone();
        clone.linearColors = (Color[])linearColors.Clone();
        clone.transform = transform.Clone();
        return clone;
    }

    internal override void ApplyTo(SKPaint paint, SKMatrix deviceMatrix)
    {
        var (colors, positions) = GradientStops.Build(linearColors[0], linearColors[1], blend, interpolationColors);
        var tileMode = WrapMode switch
        {
            WrapMode.Clamp => SKShaderTileMode.Clamp,
            WrapMode.Tile => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Mirror,
        };
        paint.Color = SKColors.Black;
        paint.Shader = SKShader.CreateLinearGradient(start, end, colors, positions, tileMode, transform.m);
    }

    private static float ModeToAngle(LinearGradientMode mode) => mode switch
    {
        LinearGradientMode.Vertical => 90,
        LinearGradientMode.ForwardDiagonal => 45,
        LinearGradientMode.BackwardDiagonal => 135,
        _ => 0,
    };

    private static void ValidateFocusScale(float focus, float scale)
    {
        if (focus < 0 || focus > 1)
            throw new ArgumentException("Parameter is not valid.", nameof(focus));
        if (scale < 0 || scale > 1)
            throw new ArgumentException("Parameter is not valid.", nameof(scale));
    }

    // Normalized normal CDF over [-2 sigma, 2 sigma], mapped to u in [0, 1].
    private static float NormalRamp(float u)
    {
        static double Phi(double x) => 0.5 * (1 + Erf(x / Math.Sqrt(2)));
        double lo = Phi(-2), hi = Phi(2);
        return (float)((Phi((u - 0.5) * 4) - lo) / (hi - lo));
    }

    // Abramowitz-Stegun 7.1.26, max error 1.5e-7.
    private static double Erf(double x)
    {
        double sign = Math.Sign(x);
        x = Math.Abs(x);
        double t = 1 / (1 + 0.3275911 * x);
        double y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }
}

/// <summary>
/// A gradient from a center color to the boundary of a path.
/// </summary>
/// <remarks>
/// Skia has no path gradient, so this is approximated with an elliptical radial gradient centered on
/// <see cref="CenterPoint"/> that reaches the surround color at the path's bounding ellipse (or its
/// circumscribed ellipse for non-elliptical paths). Only the first surround color is used.
/// </remarks>
public sealed class PathGradientBrush : Brush
{
    private readonly RectangleF bounds;
    private readonly bool isEllipse;
    private Color[] surroundColors = [Color.White];
    private Blend? blend;
    private ColorBlend? interpolationColors;

    public PathGradientBrush(PointF[] points)
        : this(points, WrapMode.Clamp)
    {
    }

    public PathGradientBrush(PointF[] points, WrapMode wrapMode)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2)
            throw new ArgumentException("At least two points are required.", nameof(points));
        bounds = GetBounds(points);
        CenterPoint = new PointF(points.Average(p => p.X), points.Average(p => p.Y));
        WrapMode = wrapMode;
    }

    public PathGradientBrush(Point[] points)
        : this(SkiaConversions.ToPointFs(points))
    {
    }

    public PathGradientBrush(Point[] points, WrapMode wrapMode)
        : this(SkiaConversions.ToPointFs(points), wrapMode)
    {
    }

    public PathGradientBrush(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        bounds = path.GetBounds();
        isEllipse = path.path.IsOval;
        CenterPoint = new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        WrapMode = WrapMode.Clamp;
    }

    public Color CenterColor { get; set; } = Color.White;

    public PointF CenterPoint { get; set; }

    public Color[] SurroundColors
    {
        get => (Color[])surroundColors.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length == 0)
                throw new ArgumentException("Parameter is not valid.", nameof(value));
            surroundColors = (Color[])value.Clone();
        }
    }

    public PointF FocusScales { get; set; }

    public RectangleF Rectangle => bounds;

    public WrapMode WrapMode { get; set; }

    public Blend? Blend
    {
        get => blend;
        set
        {
            blend = value;
            interpolationColors = null;
        }
    }

    public ColorBlend? InterpolationColors
    {
        get => interpolationColors;
        set
        {
            interpolationColors = value;
            blend = null;
        }
    }

    public Matrix Transform
    {
        get => transform.Clone();
        set => transform = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
    }

    public void ResetTransform() => transform.Reset();

    public void MultiplyTransform(Matrix matrix) => MultiplyTransformCore(matrix, MatrixOrder.Prepend);

    public void MultiplyTransform(Matrix matrix, MatrixOrder order) => MultiplyTransformCore(matrix, order);

    public void TranslateTransform(float dx, float dy, MatrixOrder order = MatrixOrder.Prepend) => transform.Translate(dx, dy, order);

    public void ScaleTransform(float sx, float sy, MatrixOrder order = MatrixOrder.Prepend) => transform.Scale(sx, sy, order);

    public void RotateTransform(float angle, MatrixOrder order = MatrixOrder.Prepend) => transform.Rotate(angle, order);

    public void SetBlendTriangularShape(float focus) => SetBlendTriangularShape(focus, 1f);

    public void SetBlendTriangularShape(float focus, float scale) =>
        Blend = new Blend(3) { Positions = [0, focus, 1], Factors = [0, scale, 0] };

    public void SetSigmaBellShape(float focus) => SetSigmaBellShape(focus, 1f);

    public void SetSigmaBellShape(float focus, float scale) => SetBlendTriangularShape(focus, scale);

    public override object Clone()
    {
        var clone = (PathGradientBrush)MemberwiseClone();
        clone.surroundColors = (Color[])surroundColors.Clone();
        clone.transform = transform.Clone();
        return clone;
    }

    internal override void ApplyTo(SKPaint paint, SKMatrix deviceMatrix)
    {
        float rx = Math.Max(CenterPoint.X - bounds.Left, bounds.Right - CenterPoint.X);
        float ry = Math.Max(CenterPoint.Y - bounds.Top, bounds.Bottom - CenterPoint.Y);
        if (!isEllipse)
        {
            rx *= MathF.Sqrt(2);
            ry *= MathF.Sqrt(2);
        }

        paint.Color = SKColors.Black;
        if (rx <= 0 || ry <= 0)
        {
            paint.Shader = null;
            paint.Color = CenterColor.ToSKColor();
            return;
        }

        // GDI+ positions run from the boundary (0) to the center (1); radial gradients run the other way.
        var (colors, positions) = GradientStops.Build(surroundColors[0], CenterColor, blend, interpolationColors);
        Array.Reverse(colors);
        Array.Reverse(positions);
        for (int i = 0; i < positions.Length; i++)
            positions[i] = 1 - positions[i];

        float focus = Math.Clamp((FocusScales.X + FocusScales.Y) / 2, 0, 1);
        if (focus > 0)
        {
            for (int i = 0; i < positions.Length; i++)
                positions[i] = focus + positions[i] * (1 - focus);
            colors = [colors[0], .. colors];
            positions = [0, .. positions];
        }

        var local = SKMatrix.Concat(
            transform.m,
            SKMatrix.CreateScaleTranslation(rx, ry, CenterPoint.X, CenterPoint.Y));
        paint.Shader = SKShader.CreateRadialGradient(SKPoint.Empty, 1, colors, positions, SKShaderTileMode.Clamp, local);
    }

    private static RectangleF GetBounds(PointF[] points)
    {
        float left = points.Min(p => p.X), top = points.Min(p => p.Y);
        float right = points.Max(p => p.X), bottom = points.Max(p => p.Y);
        return RectangleF.FromLTRB(left, top, right, bottom);
    }
}

/// <summary>Fills with one of the 53 GDI+ 8x8 hatch patterns.</summary>
/// <remarks>Like GDI+, the pattern is aligned to device pixels and is not affected by the world transform.</remarks>
public sealed class HatchBrush : Brush
{
    private SKBitmap? pattern;

    public HatchBrush(HatchStyle hatchstyle, Color foreColor)
        : this(hatchstyle, foreColor, Color.Black)
    {
    }

    public HatchBrush(HatchStyle hatchstyle, Color foreColor, Color backColor)
    {
        if (hatchstyle < HatchStyle.Horizontal || hatchstyle > HatchStyle.SolidDiamond)
            throw new ArgumentException("Parameter is not valid.", nameof(hatchstyle));
        HatchStyle = hatchstyle;
        ForegroundColor = foreColor;
        BackgroundColor = backColor;
    }

    public HatchStyle HatchStyle { get; }

    public Color ForegroundColor { get; }

    public Color BackgroundColor { get; }

    public override object Clone() => new HatchBrush(HatchStyle, ForegroundColor, BackgroundColor);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            pattern?.Dispose();
            pattern = null;
        }
        base.Dispose(disposing);
    }

    internal override void ApplyTo(SKPaint paint, SKMatrix deviceMatrix)
    {
        pattern ??= CreatePattern();
        var local = deviceMatrix.TryInvert(out var inverse) ? inverse : SKMatrix.Identity;
        paint.Color = SKColors.Black;
        paint.Shader = SKShader.CreateBitmap(pattern, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, local);
    }

    private SKBitmap CreatePattern()
    {
        var rows = HatchPatterns.Get(HatchStyle);
        var fore = ForegroundColor.ToSKColor();
        var back = BackgroundColor.ToSKColor();
        var bitmap = new SKBitmap(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul);
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
                bitmap.SetPixel(x, y, (rows[y] & (0x80 >> x)) != 0 ? fore : back);
        }
        return bitmap;
    }
}

internal static class HatchPatterns
{
    // One byte per row, most significant bit = leftmost pixel. Indexed by HatchStyle.
    private static readonly byte[][] patterns =
    [
        [0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], // Horizontal
        [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80], // Vertical
        [0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01], // ForwardDiagonal
        [0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80], // BackwardDiagonal
        [0xFF, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80], // Cross
        [0x81, 0x42, 0x24, 0x18, 0x18, 0x24, 0x42, 0x81], // DiagonalCross
        [0x80, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00], // Percent05
        [0x80, 0x00, 0x08, 0x00, 0x80, 0x00, 0x08, 0x00], // Percent10
        [0x88, 0x00, 0x22, 0x00, 0x88, 0x00, 0x22, 0x00], // Percent20
        [0x88, 0x22, 0x88, 0x22, 0x88, 0x22, 0x88, 0x22], // Percent25
        [0xAA, 0x44, 0xAA, 0x11, 0xAA, 0x44, 0xAA, 0x11], // Percent30
        [0xAA, 0x55, 0xAA, 0x51, 0xAA, 0x55, 0xAA, 0x15], // Percent40
        [0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55], // Percent50
        [0x55, 0xAA, 0x55, 0xAE, 0x55, 0xAA, 0x55, 0xEA], // Percent60
        [0x55, 0xBB, 0x55, 0xEE, 0x55, 0xBB, 0x55, 0xEE], // Percent70
        [0x77, 0xDD, 0x77, 0xDD, 0x77, 0xDD, 0x77, 0xDD], // Percent75
        [0x77, 0xFF, 0xDD, 0xFF, 0x77, 0xFF, 0xDD, 0xFF], // Percent80
        [0x7F, 0xFF, 0xF7, 0xFF, 0x7F, 0xFF, 0xF7, 0xFF], // Percent90
        [0x88, 0x44, 0x22, 0x11, 0x88, 0x44, 0x22, 0x11], // LightDownwardDiagonal
        [0x11, 0x22, 0x44, 0x88, 0x11, 0x22, 0x44, 0x88], // LightUpwardDiagonal
        [0xCC, 0x66, 0x33, 0x99, 0xCC, 0x66, 0x33, 0x99], // DarkDownwardDiagonal
        [0x33, 0x66, 0xCC, 0x99, 0x33, 0x66, 0xCC, 0x99], // DarkUpwardDiagonal
        [0xC1, 0xE0, 0x70, 0x38, 0x1C, 0x0E, 0x07, 0x83], // WideDownwardDiagonal
        [0x83, 0x07, 0x0E, 0x1C, 0x38, 0x70, 0xE0, 0xC1], // WideUpwardDiagonal
        [0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88], // LightVertical
        [0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00], // LightHorizontal
        [0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA], // NarrowVertical
        [0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00], // NarrowHorizontal
        [0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC], // DarkVertical
        [0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00], // DarkHorizontal
        [0x00, 0x00, 0x88, 0x44, 0x22, 0x11, 0x00, 0x00], // DashedDownwardDiagonal
        [0x00, 0x00, 0x11, 0x22, 0x44, 0x88, 0x00, 0x00], // DashedUpwardDiagonal
        [0xF0, 0x00, 0x00, 0x00, 0x0F, 0x00, 0x00, 0x00], // DashedHorizontal
        [0x80, 0x80, 0x80, 0x80, 0x08, 0x08, 0x08, 0x08], // DashedVertical
        [0x80, 0x08, 0x40, 0x02, 0x10, 0x01, 0x20, 0x04], // SmallConfetti
        [0xB1, 0x30, 0x03, 0x1B, 0xD8, 0xC0, 0x0C, 0x8D], // LargeConfetti
        [0x81, 0x42, 0x24, 0x18, 0x81, 0x42, 0x24, 0x18], // ZigZag
        [0x00, 0x18, 0xA4, 0x03, 0x00, 0x18, 0xA4, 0x03], // Wave
        [0x01, 0x02, 0x04, 0x08, 0x18, 0x24, 0x42, 0x81], // DiagonalBrick
        [0xFF, 0x80, 0x80, 0x80, 0xFF, 0x08, 0x08, 0x08], // HorizontalBrick
        [0x88, 0x54, 0x22, 0x45, 0x88, 0x14, 0x22, 0x51], // Weave
        [0xAA, 0x55, 0xAA, 0x55, 0xF0, 0xF0, 0xF0, 0xF0], // Plaid
        [0x00, 0x10, 0x08, 0x10, 0x00, 0x01, 0x80, 0x01], // Divot
        [0xAA, 0x00, 0x80, 0x00, 0x80, 0x00, 0x80, 0x00], // DottedGrid
        [0x80, 0x00, 0x22, 0x00, 0x08, 0x00, 0x22, 0x00], // DottedDiamond
        [0x03, 0x84, 0x48, 0x30, 0x0C, 0x02, 0x01, 0x01], // Shingle
        [0xFF, 0x66, 0xFF, 0x99, 0xFF, 0x66, 0xFF, 0x99], // Trellis
        [0xEE, 0x91, 0xF1, 0xF1, 0xEE, 0x19, 0x1F, 0x1F], // Sphere
        [0xFF, 0x88, 0x88, 0x88, 0xFF, 0x88, 0x88, 0x88], // SmallGrid
        [0x99, 0x66, 0x66, 0x99, 0x99, 0x66, 0x66, 0x99], // SmallCheckerBoard
        [0xF0, 0xF0, 0xF0, 0xF0, 0x0F, 0x0F, 0x0F, 0x0F], // LargeCheckerBoard
        [0x82, 0x44, 0x28, 0x10, 0x28, 0x44, 0x82, 0x01], // OutlinedDiamond
        [0x10, 0x38, 0x7C, 0xFE, 0x7C, 0x38, 0x10, 0x00], // SolidDiamond
    ];

    public static byte[] Get(HatchStyle style) => patterns[(int)style];
}

internal static class GradientStops
{
    /// <summary>Builds Skia gradient stops from GDI+ two-color + blend or interpolation-color settings.</summary>
    public static (SKColor[] Colors, float[] Positions) Build(Color from, Color to, Blend? blend, ColorBlend? interpolation)
    {
        if (interpolation is { Colors.Length: >= 2 } && interpolation.Positions.Length == interpolation.Colors.Length)
        {
            return (
                Array.ConvertAll(interpolation.Colors, c => c.ToSKColor()),
                (float[])interpolation.Positions.Clone());
        }

        if (blend is { Factors.Length: >= 1 } && blend.Positions.Length == blend.Factors.Length)
        {
            var colors = new SKColor[blend.Factors.Length];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = SkiaConversions.Lerp(from, to, blend.Factors[i]);
            return (colors, (float[])blend.Positions.Clone());
        }

        return ([from.ToSKColor(), to.ToSKColor()], [0f, 1f]);
    }
}
