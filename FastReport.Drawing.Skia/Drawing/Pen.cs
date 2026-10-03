using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing;

/// <summary>Defines an object used to draw lines and curves.</summary>
/// <remarks>
/// Skia strokes use a single cap for both ends, so <see cref="StartCap"/> is used for the whole stroke;
/// anchor caps (arrows, diamonds, ...) are drawn flat.
/// </remarks>
public sealed class Pen : ICloneable, IDisposable
{
    private readonly bool immutable;
    private Brush brush;
    private float width;
    private DashStyle dashStyle;
    private float[] dashPattern = [1f];

    public Pen(Color color)
        : this(color, 1f)
    {
    }

    public Pen(Color color, float width)
        : this(new SolidBrush(color), width)
    {
    }

    public Pen(Brush brush)
        : this(brush, 1f)
    {
    }

    public Pen(Brush brush, float width)
    {
        ArgumentNullException.ThrowIfNull(brush);
        this.brush = (Brush)brush.Clone();
        this.width = width;
    }

    internal Pen(Color color, bool immutable)
        : this(color, 1f)
    {
        this.immutable = immutable;
    }

    public Color Color
    {
        get => brush is SolidBrush solid ? solid.Color : Color.Empty;
        set
        {
            CheckMutable();
            brush = new SolidBrush(value);
        }
    }

    public Brush Brush
    {
        get => (Brush)brush.Clone();
        set
        {
            CheckMutable();
            brush = (Brush)(value ?? throw new ArgumentNullException(nameof(value))).Clone();
        }
    }

    public PenType PenType => brush switch
    {
        SolidBrush => PenType.SolidColor,
        HatchBrush => PenType.HatchFill,
        TextureBrush => PenType.TextureFill,
        PathGradientBrush => PenType.PathGradient,
        LinearGradientBrush => PenType.LinearGradient,
        _ => PenType.SolidColor,
    };

    public float Width
    {
        get => width;
        set
        {
            CheckMutable();
            width = value;
        }
    }

    public DashStyle DashStyle
    {
        get => dashStyle;
        set
        {
            CheckMutable();
            dashStyle = value;
        }
    }

    /// <summary>Dash and gap lengths, in multiples of the pen width.</summary>
    public float[] DashPattern
    {
        get => (float[])dashPattern.Clone();
        set
        {
            CheckMutable();
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length == 0 || value.Any(v => v <= 0))
                throw new ArgumentException("Parameter is not valid.", nameof(value));
            dashPattern = (float[])value.Clone();
            dashStyle = DashStyle.Custom;
        }
    }

    public float DashOffset { get; set; }

    public DashCap DashCap { get; set; }

    public LineCap StartCap { get; set; }

    public LineCap EndCap { get; set; }

    public LineJoin LineJoin { get; set; }

    public float MiterLimit { get; set; } = 10f;

    public PenAlignment Alignment { get; set; }

    public float[] CompoundArray { get; set; } = [];

    public Matrix Transform { get; set; } = new();

    public void SetLineCap(LineCap startCap, LineCap endCap, DashCap dashCap)
    {
        StartCap = startCap;
        EndCap = endCap;
        DashCap = dashCap;
    }

    public void ResetTransform() => Transform = new Matrix();

    public object Clone() => new Pen(brush, width)
    {
        dashStyle = dashStyle,
        dashPattern = (float[])dashPattern.Clone(),
        DashOffset = DashOffset,
        DashCap = DashCap,
        StartCap = StartCap,
        EndCap = EndCap,
        LineJoin = LineJoin,
        MiterLimit = MiterLimit,
        Alignment = Alignment,
        CompoundArray = (float[])CompoundArray.Clone(),
        Transform = Transform.Clone(),
    };

    public void Dispose()
    {
    }

    internal void ApplyTo(SKPaint paint, SKMatrix deviceMatrix)
    {
        brush.ApplyTo(paint, deviceMatrix);
        paint.Style = SKPaintStyle.Stroke;
        // A zero width means a one-device-pixel line in GDI+, which is exactly Skia's hairline.
        paint.StrokeWidth = width;
        paint.StrokeJoin = LineJoin switch
        {
            LineJoin.Bevel => SKStrokeJoin.Bevel,
            LineJoin.Round => SKStrokeJoin.Round,
            _ => SKStrokeJoin.Miter,
        };
        paint.StrokeMiter = MiterLimit;
        paint.StrokeCap = (StartCap & ~LineCap.AnchorMask) switch
        {
            LineCap.Square => SKStrokeCap.Square,
            LineCap.Round => SKStrokeCap.Round,
            _ => SKStrokeCap.Butt,
        };

        var pattern = GetDashPattern();
        if (pattern != null)
        {
            float scale = width > 0 ? width : 1f;
            int length = pattern.Length % 2 == 0 ? pattern.Length : pattern.Length * 2;
            var intervals = new float[length];
            for (int i = 0; i < length; i++)
                intervals[i] = pattern[i % pattern.Length] * scale;

            paint.PathEffect = SKPathEffect.CreateDash(intervals, DashOffset * scale);
            paint.StrokeCap = DashCap == DashCap.Round ? SKStrokeCap.Round : SKStrokeCap.Butt;
        }
        else
        {
            paint.PathEffect = null;
        }
    }

    private float[]? GetDashPattern() => dashStyle switch
    {
        DashStyle.Dash => [3, 1],
        DashStyle.Dot => [1, 1],
        DashStyle.DashDot => [3, 1, 1, 1],
        DashStyle.DashDotDot => [3, 1, 1, 1, 1, 1],
        DashStyle.Custom => dashPattern,
        _ => null,
    };

    private void CheckMutable()
    {
        if (immutable)
            throw new ArgumentException("Changes cannot be made to Pen because permissions are not valid.");
    }
}
