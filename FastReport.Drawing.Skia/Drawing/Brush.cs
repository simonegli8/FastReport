using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing;

/// <summary>Base class for objects that fill the interiors of shapes.</summary>
public abstract class Brush : ICloneable, IDisposable
{
    private protected Matrix transform = new();

    public abstract object Clone();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>
    /// Configures <paramref name="paint"/> (color or shader) to paint with this brush.
    /// <paramref name="deviceMatrix"/> is the canvas total matrix, for brushes aligned to device pixels.
    /// </summary>
    internal abstract void ApplyTo(SKPaint paint, SKMatrix deviceMatrix);

    private protected void MultiplyTransformCore(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        transform.Multiply(matrix, order);
    }

    private protected static SKShaderTileMode ToTileModeX(WrapMode mode) => mode switch
    {
        WrapMode.TileFlipX or WrapMode.TileFlipXY => SKShaderTileMode.Mirror,
        WrapMode.Clamp => SKShaderTileMode.Decal,
        _ => SKShaderTileMode.Repeat,
    };

    private protected static SKShaderTileMode ToTileModeY(WrapMode mode) => mode switch
    {
        WrapMode.TileFlipY or WrapMode.TileFlipXY => SKShaderTileMode.Mirror,
        WrapMode.Clamp => SKShaderTileMode.Decal,
        _ => SKShaderTileMode.Repeat,
    };
}

public sealed class SolidBrush : Brush
{
    private readonly bool immutable;
    private Color color;

    public SolidBrush(Color color)
    {
        this.color = color;
    }

    internal SolidBrush(Color color, bool immutable)
    {
        this.color = color;
        this.immutable = immutable;
    }

    public Color Color
    {
        get => color;
        set
        {
            if (immutable)
                throw new ArgumentException("Changes cannot be made to Brush because permissions are not valid.");
            color = value;
        }
    }

    public override object Clone() => new SolidBrush(color);

    internal override void ApplyTo(SKPaint paint, SKMatrix deviceMatrix)
    {
        paint.Shader = null;
        paint.Color = color.ToSKColor();
    }
}

/// <summary>Fills with a (tiled) image.</summary>
public sealed class TextureBrush : Brush
{
    private readonly Bitmap image;

    public TextureBrush(Image bitmap)
        : this(bitmap, WrapMode.Tile)
    {
    }

    public TextureBrush(Image image, WrapMode wrapMode)
    {
        ArgumentNullException.ThrowIfNull(image);
        this.image = (Bitmap)image.Clone();
        WrapMode = wrapMode;
    }

    public TextureBrush(Image image, RectangleF dstRect)
        : this(image, WrapMode.Tile, dstRect)
    {
    }

    public TextureBrush(Image image, WrapMode wrapMode, RectangleF dstRect)
    {
        ArgumentNullException.ThrowIfNull(image);
        // As in GDI+, the rectangle selects the portion of the image that is used as the texture.
        var bounds = Rectangle.Intersect(Rectangle.Round(dstRect), new Rectangle(0, 0, image.Width, image.Height));
        this.image = bounds.Width > 0 && bounds.Height > 0
            ? ((Bitmap)image).Clone(bounds, image.PixelFormat)
            : (Bitmap)image.Clone();
        WrapMode = wrapMode;
    }

    public Image Image => (Image)image.Clone();

    public WrapMode WrapMode { get; set; }

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

    public override object Clone()
    {
        var clone = new TextureBrush(image, WrapMode);
        clone.transform = transform.Clone();
        return clone;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            image.Dispose();
        base.Dispose(disposing);
    }

    internal override void ApplyTo(SKPaint paint, SKMatrix deviceMatrix)
    {
        paint.Color = SKColors.Black;
        paint.Shader = image.GetSKImage().ToShader(
            ToTileModeX(WrapMode),
            ToTileModeY(WrapMode),
            new SKSamplingOptions(SKFilterMode.Linear),
            transform.m);
    }
}
