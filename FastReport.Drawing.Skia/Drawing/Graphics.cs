using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// A GDI+-compatible drawing surface on top of an <see cref="SKCanvas"/>.
/// </summary>
/// <remarks>
/// <para>
/// GDI+ state (world transform, page unit/scale, clip, rendering hints) is kept here rather than in the
/// canvas' save stack, because GDI+ allows operations Skia cannot express incrementally: replacing or
/// widening the clip (Skia clips only shrink), and <see cref="Restore"/> popping several saved states at
/// once. Whenever the clip changes, the canvas is rewound to its initial state and the clip re-applied.
/// </para>
/// <para>
/// Coordinates go through world → page (<see cref="PageUnit"/>, <see cref="PageScale"/>) → device. The
/// device space is the canvas' own coordinate space at the time this object was created.
/// </para>
/// </remarks>
public sealed partial class Graphics : IDisposable
{
    private readonly SKCanvas canvas;
    private readonly bool ownsCanvas;
    private readonly Bitmap? target;
    private readonly int baseSaveCount;
    private readonly SKMatrix baseMatrix;
    private readonly SKMatrix halfPixelShift;
    private readonly List<State> savedStates = [];

    private SKMatrix world = SKMatrix.Identity;
    private SKPath? clip; // In device space; null means infinite.
    private GraphicsUnit pageUnit = GraphicsUnit.Display;
    private float pageScale = 1f;
    private PixelOffsetMode pixelOffsetMode = PixelOffsetMode.Default;
    private bool disposed;

    private Graphics(SKCanvas canvas, bool ownsCanvas, Bitmap? target, float dpiX, float dpiY)
    {
        this.canvas = canvas;
        this.ownsCanvas = ownsCanvas;
        this.target = target;
        DpiX = dpiX;
        DpiY = dpiY;
        baseSaveCount = canvas.SaveCount;
        baseMatrix = canvas.TotalMatrix;
        // A translation by half a device pixel, expressed in the canvas' coordinate space.
        halfPixelShift = baseMatrix.TryInvert(out var inverse)
            ? baseMatrix.PreConcat(SKMatrix.CreateTranslation(0.5f, 0.5f)).PreConcat(inverse)
            : SKMatrix.CreateTranslation(0.5f, 0.5f);
        ApplyCanvasState();
    }

    #region Factories

    /// <summary>Creates a graphics that draws onto a <see cref="Bitmap"/>, using its resolution as DPI.</summary>
    public static Graphics FromImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image is not Bitmap bitmap)
            throw new ArgumentException("Only Bitmap images are supported.", nameof(image));
        if ((bitmap.PixelFormat & PixelFormat.Indexed) != 0)
            throw new ArgumentException("A Graphics object cannot be created from an image that has an indexed pixel format.", nameof(image));

        return new Graphics(new SKCanvas(bitmap.GetSKBitmap()), true, bitmap, bitmap.HorizontalResolution, bitmap.VerticalResolution);
    }

    /// <summary>
    /// Creates a graphics over an existing canvas (raster, PDF, SVG or picture recorder). The canvas is not
    /// disposed with the graphics, and its state is restored when the graphics is disposed.
    /// </summary>
    public static Graphics FromCanvas(SKCanvas canvas, float dpiX = 96f, float dpiY = 96f)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        return new Graphics(canvas, false, null, dpiX, dpiY);
    }

    #endregion

    #region Properties

    /// <summary>The underlying Skia canvas.</summary>
    public SKCanvas Canvas => canvas;

    public float DpiX { get; }

    public float DpiY { get; }

    public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.None;

    public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.SystemDefault;

    public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Bilinear;

    public CompositingQuality CompositingQuality { get; set; } = CompositingQuality.Default;

    public CompositingMode CompositingMode { get; set; } = CompositingMode.SourceOver;

    public int TextContrast { get; set; } = 4;

    public Point RenderingOrigin { get; set; }

    /// <summary>
    /// With the default (<see cref="PixelOffsetMode.None"/>) pixel centers sit on integer coordinates as in
    /// GDI+, so antialiased geometry is shifted by half a device pixel relative to Skia (a 1px line at an
    /// integer coordinate stays crisp). <see cref="PixelOffsetMode.Half"/> uses Skia's native pixel grid.
    /// Aliased geometry, text, images and clipping are unaffected, since both libraries already agree there.
    /// </summary>
    public PixelOffsetMode PixelOffsetMode
    {
        get => pixelOffsetMode;
        set => pixelOffsetMode = value;
    }

    public GraphicsUnit PageUnit
    {
        get => pageUnit;
        set
        {
            if (value == GraphicsUnit.World)
                throw new ArgumentException("GraphicsUnit.World is not a valid page unit.", nameof(value));
            pageUnit = value;
            ApplyMatrix();
        }
    }

    public float PageScale
    {
        get => pageScale;
        set
        {
            if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Parameter is not valid.", nameof(value));
            pageScale = value;
            ApplyMatrix();
        }
    }

    public Matrix Transform
    {
        get => new(world);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            world = value.m;
            ApplyMatrix();
        }
    }

    /// <summary>The clip region in world coordinates.</summary>
    public Region Clip
    {
        get => new(clip == null ? null : ToWorld(clip));
        set => SetClip(value ?? throw new ArgumentNullException(nameof(value)), CombineMode.Replace);
    }

    public RectangleF ClipBounds
    {
        get
        {
            using var region = Clip;
            return region.GetBounds(this);
        }
    }

    public RectangleF VisibleClipBounds => canvas.LocalClipBounds.ToRectangleF();

    public bool IsClipEmpty => clip != null && Region.IsPathEmpty(clip);

    public bool IsVisibleClipEmpty => canvas.IsClipEmpty;

    private bool Antialias => SmoothingMode is SmoothingMode.AntiAlias or SmoothingMode.HighQuality;

    private bool UsesHalfPixelOffset =>
        Antialias && pixelOffsetMode is not (PixelOffsetMode.Half or PixelOffsetMode.HighQuality);

    private float PixelsPerUnitX => SkiaConversions.PixelsPerUnit(pageUnit, DpiX) * pageScale;

    private float PixelsPerUnitY => SkiaConversions.PixelsPerUnit(pageUnit, DpiY) * pageScale;

    private SKMatrix PageMatrix => SKMatrix.CreateScale(PixelsPerUnitX, PixelsPerUnitY);

    /// <summary>World to device transform (excluding the canvas' initial matrix).</summary>
    private SKMatrix DeviceMatrix => PageMatrix.PreConcat(world);

    private SKSamplingOptions Sampling => InterpolationMode switch
    {
        InterpolationMode.NearestNeighbor => new SKSamplingOptions(SKFilterMode.Nearest),
        InterpolationMode.High or InterpolationMode.Bicubic or InterpolationMode.HighQualityBicubic => new SKSamplingOptions(SKCubicResampler.Mitchell),
        InterpolationMode.HighQualityBilinear => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
        _ => new SKSamplingOptions(SKFilterMode.Linear),
    };

    private SKFontEdging FontEdging => TextRenderingHint switch
    {
        TextRenderingHint.SingleBitPerPixel or TextRenderingHint.SingleBitPerPixelGridFit => SKFontEdging.Alias,
        TextRenderingHint.ClearTypeGridFit => SKFontEdging.SubpixelAntialias,
        _ => SKFontEdging.Antialias,
    };

    private SKFontHinting FontHinting => TextRenderingHint switch
    {
        TextRenderingHint.AntiAlias or TextRenderingHint.SingleBitPerPixel => SKFontHinting.None,
        TextRenderingHint.SystemDefault => SKFontHinting.Slight,
        _ => SKFontHinting.Normal,
    };

    #endregion

    #region Text

    public void DrawString(string? s, Font font, Brush brush, float x, float y) =>
        DrawString(s, font, brush, new RectangleF(x, y, 0, 0), null);

    public void DrawString(string? s, Font font, Brush brush, PointF point) =>
        DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), null);

    public void DrawString(string? s, Font font, Brush brush, float x, float y, StringFormat? format) =>
        DrawString(s, font, brush, new RectangleF(x, y, 0, 0), format);

    public void DrawString(string? s, Font font, Brush brush, PointF point, StringFormat? format) =>
        DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), format);

    public void DrawString(string? s, Font font, Brush brush, RectangleF layoutRectangle) =>
        DrawString(s, font, brush, layoutRectangle, null);

    public void DrawString(string? s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(brush);
        if (s is not { Length: > 0 })
            return;

        Touch();
        bool vertical = IsVertical(format);
        using var textFont = CreateTextFont(font);
        var layout = new TextLayout(s, textFont, LayoutSize(layoutRectangle.Size, vertical), format);
        using var paint = CreatePaint(brush);

        int saveCount = canvas.Save();
        bool clipToRect = (format == null || (format.FormatFlags & StringFormatFlags.NoClip) == 0)
            && layoutRectangle.Width > 0 && layoutRectangle.Height > 0;
        if (clipToRect)
            canvas.ClipRect(layoutRectangle.ToSKRect(), SKClipOperation.Intersect, false);

        var localRect = layoutRectangle;
        if (vertical)
        {
            canvas.Translate(layoutRectangle.Right, layoutRectangle.Top);
            canvas.RotateDegrees(90);
            localRect = new RectangleF(0, 0, layoutRectangle.Height, layoutRectangle.Width);
        }

        layout.Draw(canvas, localRect, paint);
        canvas.RestoreToCount(saveCount);
    }

    public SizeF MeasureString(string? text, Font font) =>
        MeasureString(text, font, SizeF.Empty, null, out _, out _);

    public SizeF MeasureString(string? text, Font font, int width) =>
        MeasureString(text, font, new SizeF(width, 0), null, out _, out _);

    public SizeF MeasureString(string? text, Font font, SizeF layoutArea) =>
        MeasureString(text, font, layoutArea, null, out _, out _);

    public SizeF MeasureString(string? text, Font font, int width, StringFormat? format) =>
        MeasureString(text, font, new SizeF(width, 0), format, out _, out _);

    public SizeF MeasureString(string? text, Font font, PointF origin, StringFormat? stringFormat) =>
        MeasureString(text, font, SizeF.Empty, stringFormat, out _, out _);

    public SizeF MeasureString(string? text, Font font, SizeF layoutArea, StringFormat? stringFormat) =>
        MeasureString(text, font, layoutArea, stringFormat, out _, out _);

    public SizeF MeasureString(string? text, Font font, SizeF layoutArea, StringFormat? stringFormat, out int charactersFitted, out int linesFilled)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (text is not { Length: > 0 })
        {
            charactersFitted = 0;
            linesFilled = 0;
            return SizeF.Empty;
        }

        bool vertical = IsVertical(stringFormat);
        using var textFont = CreateTextFont(font);
        var layout = new TextLayout(text, textFont, LayoutSize(layoutArea, vertical), stringFormat);
        charactersFitted = layout.CharactersFitted;
        linesFilled = layout.VisibleLineCount;
        var size = layout.Size;
        return vertical ? new SizeF(size.Height, size.Width) : size;
    }

    public Region[] MeasureCharacterRanges(string? text, Font font, RectangleF layoutRect, StringFormat? stringFormat)
    {
        ArgumentNullException.ThrowIfNull(font);
        var ranges = stringFormat?.MeasurableCharacterRanges ?? [];
        var regions = new Region[ranges.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            regions[i] = new Region();
            regions[i].MakeEmpty();
        }

        if (text is not { Length: > 0 } || ranges.Length == 0)
            return regions;

        bool vertical = IsVertical(stringFormat);
        using var textFont = CreateTextFont(font);
        var layout = new TextLayout(text, textFont, LayoutSize(layoutRect.Size, vertical), stringFormat);
        var localRect = vertical ? new RectangleF(0, 0, layoutRect.Height, layoutRect.Width) : layoutRect;

        for (int i = 0; i < ranges.Length; i++)
        {
            foreach (var bounds in layout.GetRangeBounds(localRect, ranges[i]))
            {
                // Vertical text is laid out rotated 90° clockwise around the rectangle's top-right corner.
                var rect = vertical
                    ? new RectangleF(layoutRect.Right - bounds.Bottom, layoutRect.Top + bounds.Left, bounds.Height, bounds.Width)
                    : bounds;
                regions[i].Union(rect);
            }
        }

        return regions;
    }

    /// <summary>The em size of <paramref name="font"/> in this graphics' world units.</summary>
    internal float GetFontEmSize(Font font) =>
        font.Unit == GraphicsUnit.World ? font.Size : font.GetSizeInPixels(DpiY) / PixelsPerUnitY;

    private TextFont CreateTextFont(Font font) => new(font, GetFontEmSize(font), FontEdging, FontHinting);

    private static bool IsVertical(StringFormat? format) =>
        format != null && (format.FormatFlags & StringFormatFlags.DirectionVertical) != 0;

    private static SizeF LayoutSize(SizeF size, bool vertical) => vertical ? new SizeF(size.Height, size.Width) : size;

    #endregion

    #region Images

    public void DrawImage(Image image, PointF point) => DrawImage(image, point.X, point.Y);

    public void DrawImage(Image image, Point point) => DrawImage(image, point.X, point.Y);

    public void DrawImage(Image image, int x, int y) => DrawImage(image, (float)x, y);

    /// <summary>Draws the image at its physical size (pixel size adjusted for the image and device resolution).</summary>
    public void DrawImage(Image image, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(image);
        var size = PhysicalSize(image);
        DrawImage(image, x, y, size.Width, size.Height);
    }

    public void DrawImage(Image image, RectangleF rect) => DrawImage(image, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawImage(Image image, Rectangle rect) => DrawImage(image, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawImage(Image image, int x, int y, int width, int height) => DrawImage(image, (float)x, y, width, height);

    public void DrawImage(Image image, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(image);
        DrawImageCore(image, new SKRect(0, 0, image.Width, image.Height), SKRect.Create(x, y, width, height), null, null);
    }

    public void DrawImage(Image image, float x, float y, RectangleF srcRect, GraphicsUnit srcUnit)
    {
        ArgumentNullException.ThrowIfNull(image);
        DrawImageCore(image, srcRect.ToSKRect(), SKRect.Create(x, y, srcRect.Width, srcRect.Height), null, null);
    }

    public void DrawImage(Image image, int x, int y, Rectangle srcRect, GraphicsUnit srcUnit) =>
        DrawImage(image, (float)x, y, srcRect, srcUnit);

    public void DrawImage(Image image, RectangleF destRect, RectangleF srcRect, GraphicsUnit srcUnit) =>
        DrawImageCore(image, srcRect.ToSKRect(), destRect.ToSKRect(), null, null);

    public void DrawImage(Image image, Rectangle destRect, Rectangle srcRect, GraphicsUnit srcUnit) =>
        DrawImageCore(image, srcRect.ToSKRect(), destRect.ToSKRect(), null, null);

    public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit) =>
        DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, null);

    public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit, ImageAttributes? imageAttr) =>
        DrawImageCore(image, SKRect.Create(srcX, srcY, srcWidth, srcHeight), destRect.ToSKRect(), imageAttr, null);

    public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit) =>
        DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, null);

    public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes? imageAttrs) =>
        DrawImageCore(image, SKRect.Create(srcX, srcY, srcWidth, srcHeight), destRect.ToSKRect(), imageAttrs, null);

    public void DrawImage(Image image, PointF[] destPoints)
    {
        ArgumentNullException.ThrowIfNull(image);
        DrawImage(image, destPoints, new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, null);
    }

    public void DrawImage(Image image, Point[] destPoints) => DrawImage(image, SkiaConversions.ToPointFs(destPoints));

    public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect, GraphicsUnit srcUnit) =>
        DrawImage(image, destPoints, srcRect, srcUnit, null);

    /// <summary>Draws the image into the parallelogram given by its upper-left, upper-right and lower-left corners.</summary>
    public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect, GraphicsUnit srcUnit, ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(destPoints);
        if (destPoints.Length != 3)
            throw new ArgumentException("Parameter is not valid.", nameof(destPoints));
        var mapping = Matrix.MapRectToParallelogram(srcRect, destPoints[0], destPoints[1], destPoints[2]);
        DrawImageCore(image, srcRect.ToSKRect(), srcRect.ToSKRect(), imageAttr, mapping);
    }

    public void DrawImageUnscaled(Image image, Point point) => DrawImage(image, point.X, point.Y);

    public void DrawImageUnscaled(Image image, int x, int y) => DrawImage(image, x, y);

    public void DrawImageUnscaled(Image image, Rectangle rect) => DrawImage(image, rect.X, rect.Y);

    public void DrawImageUnscaled(Image image, int x, int y, int width, int height) => DrawImage(image, x, y);

    public void DrawImageUnscaledAndClipped(Image image, Rectangle rect)
    {
        ArgumentNullException.ThrowIfNull(image);
        int saveCount = canvas.Save();
        canvas.ClipRect(rect.ToSKRect());
        DrawImage(image, rect.X, rect.Y, image.Width, image.Height);
        canvas.RestoreToCount(saveCount);
    }

    private SizeF PhysicalSize(Image image) => new(
        image.Width * DpiX / image.HorizontalResolution / PixelsPerUnitX,
        image.Height * DpiY / image.VerticalResolution / PixelsPerUnitY);

    private void DrawImageCore(Image image, SKRect source, SKRect destination, ImageAttributes? attributes, SKMatrix? mapping)
    {
        ArgumentNullException.ThrowIfNull(image);
        Touch();

        if (image is VectorImage vector)
        {
            // Replays the drawing commands, so vector targets (PDF, SVG) keep vectors. A color key needs pixels
            // and is not applied.
            using var vectorColorFilter = attributes?.CreateColorFilter();
            using var vectorPaint = vectorColorFilter == null ? null : new SKPaint { ColorFilter = vectorColorFilter };
            int vectorSaveCount = canvas.Save();
            if (mapping is SKMatrix vectorMapping)
                canvas.Concat(vectorMapping);
            vector.Draw(canvas, source, destination, vectorPaint);
            canvas.RestoreToCount(vectorSaveCount);
            return;
        }

        var skImage = image.GetSKImage();
        using var keyed = attributes?.ApplyColorKey(skImage);
        using var colorFilter = attributes?.CreateColorFilter();
        using var paint = new SKPaint { IsAntialias = Antialias, ColorFilter = colorFilter };
        ApplyCompositing(paint);

        int saveCount = canvas.Save();
        if (mapping is SKMatrix extra)
            canvas.Concat(extra);
        canvas.DrawImage(keyed ?? skImage, source, destination, Sampling, paint);
        canvas.RestoreToCount(saveCount);
    }

    #endregion

    #region Lines and outlines

    public void DrawLine(Pen pen, PointF pt1, PointF pt2) => DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);

    public void DrawLine(Pen pen, Point pt1, Point pt2) => DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);

    public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) => DrawLine(pen, (float)x1, y1, x2, y2);

    public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
    {
        using var paint = CreatePaint(pen);
        DrawGeometry(() => canvas.DrawLine(x1, y1, x2, y2, paint));
    }

    public void DrawLines(Pen pen, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        using var path = BuildPath(b => b.AddPoly(SkiaConversions.ToSKPoints(points), close: false));
        StrokePath(pen, path);
    }

    public void DrawLines(Pen pen, Point[] points) => DrawLines(pen, SkiaConversions.ToPointFs(points));

    public void DrawRectangle(Pen pen, RectangleF rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawRectangle(Pen pen, Rectangle rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawRectangle(Pen pen, int x, int y, int width, int height) => DrawRectangle(pen, (float)x, y, width, height);

    public void DrawRectangle(Pen pen, float x, float y, float width, float height)
    {
        using var paint = CreatePaint(pen);
        DrawGeometry(() => canvas.DrawRect(SKRect.Create(x, y, width, height), paint));
    }

    public void DrawRectangles(Pen pen, RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
            DrawRectangle(pen, rect);
    }

    public void DrawRectangles(Pen pen, Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
            DrawRectangle(pen, rect);
    }

    public void DrawEllipse(Pen pen, RectangleF rect) => DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawEllipse(Pen pen, Rectangle rect) => DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawEllipse(Pen pen, int x, int y, int width, int height) => DrawEllipse(pen, (float)x, y, width, height);

    public void DrawEllipse(Pen pen, float x, float y, float width, float height)
    {
        using var paint = CreatePaint(pen);
        DrawGeometry(() => canvas.DrawOval(SKRect.Create(x, y, width, height), paint));
    }

    public void DrawArc(Pen pen, RectangleF rect, float startAngle, float sweepAngle) =>
        DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void DrawArc(Pen pen, Rectangle rect, float startAngle, float sweepAngle) =>
        DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void DrawArc(Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle) =>
        DrawArc(pen, (float)x, y, width, height, startAngle, sweepAngle);

    public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        using var path = BuildPath(b => GraphicsPath.AppendArc(b, SKRect.Create(x, y, width, height), startAngle, sweepAngle, connect: false));
        StrokePath(pen, path);
    }

    public void DrawPie(Pen pen, RectangleF rect, float startAngle, float sweepAngle) =>
        DrawPie(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void DrawPie(Pen pen, Rectangle rect, float startAngle, float sweepAngle) =>
        DrawPie(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void DrawPie(Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle) =>
        DrawPie(pen, (float)x, y, width, height, startAngle, sweepAngle);

    public void DrawPie(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        using var path = BuildPath(b => GraphicsPath.AppendPie(b, SKRect.Create(x, y, width, height), startAngle, sweepAngle));
        StrokePath(pen, path);
    }

    public void DrawPolygon(Pen pen, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        using var path = BuildPath(b => b.AddPoly(SkiaConversions.ToSKPoints(points), close: true));
        StrokePath(pen, path);
    }

    public void DrawPolygon(Pen pen, Point[] points) => DrawPolygon(pen, SkiaConversions.ToPointFs(points));

    public void DrawBezier(Pen pen, PointF pt1, PointF pt2, PointF pt3, PointF pt4) =>
        DrawBezier(pen, pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);

    public void DrawBezier(Pen pen, Point pt1, Point pt2, Point pt3, Point pt4) =>
        DrawBezier(pen, pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);

    public void DrawBezier(Pen pen, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
        using var path = BuildPath(b =>
        {
            b.MoveTo(x1, y1);
            b.CubicTo(x2, y2, x3, y3, x4, y4);
        });
        StrokePath(pen, path);
    }

    public void DrawBeziers(Pen pen, PointF[] points)
    {
        using var path = new GraphicsPath();
        path.AddBeziers(points);
        StrokePath(pen, path.path);
    }

    public void DrawBeziers(Pen pen, Point[] points) => DrawBeziers(pen, SkiaConversions.ToPointFs(points));

    public void DrawCurve(Pen pen, PointF[] points) => DrawCurve(pen, points, 0.5f);

    public void DrawCurve(Pen pen, Point[] points) => DrawCurve(pen, SkiaConversions.ToPointFs(points), 0.5f);

    public void DrawCurve(Pen pen, Point[] points, float tension) => DrawCurve(pen, SkiaConversions.ToPointFs(points), tension);

    public void DrawCurve(Pen pen, PointF[] points, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        DrawCurve(pen, points, 0, points.Length - 1, tension);
    }

    public void DrawCurve(Pen pen, PointF[] points, int offset, int numberOfSegments) =>
        DrawCurve(pen, points, offset, numberOfSegments, 0.5f);

    public void DrawCurve(Pen pen, Point[] points, int offset, int numberOfSegments, float tension) =>
        DrawCurve(pen, SkiaConversions.ToPointFs(points), offset, numberOfSegments, tension);

    public void DrawCurve(Pen pen, PointF[] points, int offset, int numberOfSegments, float tension)
    {
        using var path = BuildPath(b => GraphicsPath.AppendCardinalSpline(b, points, offset, numberOfSegments, tension, closed: false, connect: false));
        StrokePath(pen, path);
    }

    public void DrawClosedCurve(Pen pen, PointF[] points) => DrawClosedCurve(pen, points, 0.5f, FillMode.Alternate);

    public void DrawClosedCurve(Pen pen, Point[] points) => DrawClosedCurve(pen, SkiaConversions.ToPointFs(points), 0.5f, FillMode.Alternate);

    public void DrawClosedCurve(Pen pen, PointF[] points, float tension, FillMode fillmode)
    {
        ArgumentNullException.ThrowIfNull(points);
        using var path = BuildPath(b => GraphicsPath.AppendCardinalSpline(b, points, 0, points.Length, tension, closed: true, connect: false));
        StrokePath(pen, path);
    }

    public void DrawPath(Pen pen, GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        StrokePath(pen, path.path);
    }

    #endregion

    #region Fills

    public void Clear(Color color)
    {
        Touch();
        canvas.DrawColor(color.ToSKColor(), SKBlendMode.Src);
    }

    public void FillRectangle(Brush brush, RectangleF rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

    public void FillRectangle(Brush brush, Rectangle rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

    public void FillRectangle(Brush brush, int x, int y, int width, int height) => FillRectangle(brush, (float)x, y, width, height);

    public void FillRectangle(Brush brush, float x, float y, float width, float height)
    {
        using var paint = CreatePaint(brush);
        DrawGeometry(() => canvas.DrawRect(SKRect.Create(x, y, width, height), paint));
    }

    public void FillRectangles(Brush brush, RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
            FillRectangle(brush, rect);
    }

    public void FillRectangles(Brush brush, Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var rect in rects)
            FillRectangle(brush, rect);
    }

    public void FillEllipse(Brush brush, RectangleF rect) => FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);

    public void FillEllipse(Brush brush, Rectangle rect) => FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);

    public void FillEllipse(Brush brush, int x, int y, int width, int height) => FillEllipse(brush, (float)x, y, width, height);

    public void FillEllipse(Brush brush, float x, float y, float width, float height)
    {
        using var paint = CreatePaint(brush);
        DrawGeometry(() => canvas.DrawOval(SKRect.Create(x, y, width, height), paint));
    }

    public void FillPie(Brush brush, Rectangle rect, float startAngle, float sweepAngle) =>
        FillPie(brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void FillPie(Brush brush, RectangleF rect, float startAngle, float sweepAngle) =>
        FillPie(brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void FillPie(Brush brush, int x, int y, int width, int height, int startAngle, int sweepAngle) =>
        FillPie(brush, (float)x, y, width, height, startAngle, sweepAngle);

    public void FillPie(Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        using var path = BuildPath(b => GraphicsPath.AppendPie(b, SKRect.Create(x, y, width, height), startAngle, sweepAngle));
        FillSKPath(brush, path);
    }

    public void FillPolygon(Brush brush, PointF[] points) => FillPolygon(brush, points, FillMode.Alternate);

    public void FillPolygon(Brush brush, Point[] points) => FillPolygon(brush, SkiaConversions.ToPointFs(points), FillMode.Alternate);

    public void FillPolygon(Brush brush, Point[] points, FillMode fillMode) => FillPolygon(brush, SkiaConversions.ToPointFs(points), fillMode);

    public void FillPolygon(Brush brush, PointF[] points, FillMode fillMode)
    {
        ArgumentNullException.ThrowIfNull(points);
        using var path = BuildPath(b => b.AddPoly(SkiaConversions.ToSKPoints(points), close: true), ToFillType(fillMode));
        FillSKPath(brush, path);
    }

    public void FillClosedCurve(Brush brush, PointF[] points) => FillClosedCurve(brush, points, FillMode.Alternate, 0.5f);

    public void FillClosedCurve(Brush brush, Point[] points) => FillClosedCurve(brush, SkiaConversions.ToPointFs(points), FillMode.Alternate, 0.5f);

    public void FillClosedCurve(Brush brush, PointF[] points, FillMode fillmode) => FillClosedCurve(brush, points, fillmode, 0.5f);

    public void FillClosedCurve(Brush brush, PointF[] points, FillMode fillmode, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        using var path = BuildPath(b => GraphicsPath.AppendCardinalSpline(b, points, 0, points.Length, tension, closed: true, connect: false), ToFillType(fillmode));
        FillSKPath(brush, path);
    }

    public void FillPath(Brush brush, GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        FillSKPath(brush, path.path);
    }

    public void FillRegion(Brush brush, Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region.path != null)
        {
            FillSKPath(brush, region.path);
            return;
        }

        using var paint = CreatePaint(brush);
        Touch();
        canvas.DrawPaint(paint);
    }

    #endregion

    #region Transform

    public void ResetTransform()
    {
        world = SKMatrix.Identity;
        ApplyMatrix();
    }

    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    public void MultiplyTransform(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        world = Matrix.Combine(world, matrix.m, order);
        ApplyMatrix();
    }

    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    public void TranslateTransform(float dx, float dy, MatrixOrder order)
    {
        world = Matrix.Combine(world, SKMatrix.CreateTranslation(dx, dy), order);
        ApplyMatrix();
    }

    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    public void ScaleTransform(float sx, float sy, MatrixOrder order)
    {
        world = Matrix.Combine(world, SKMatrix.CreateScale(sx, sy), order);
        ApplyMatrix();
    }

    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    public void RotateTransform(float angle, MatrixOrder order)
    {
        world = Matrix.Combine(world, SKMatrix.CreateRotationDegrees(angle), order);
        ApplyMatrix();
    }

    public void TransformPoints(CoordinateSpace destSpace, CoordinateSpace srcSpace, PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        var toDevice = SpaceToDevice(srcSpace);
        var fromDevice = SpaceToDevice(destSpace).TryInvert(out var inverse) ? inverse : SKMatrix.Identity;
        var matrix = fromDevice.PreConcat(toDevice);
        for (int i = 0; i < pts.Length; i++)
            pts[i] = matrix.MapPoint(pts[i].X, pts[i].Y).ToPointF();
    }

    public void TransformPoints(CoordinateSpace destSpace, CoordinateSpace srcSpace, Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        var points = SkiaConversions.ToPointFs(pts);
        TransformPoints(destSpace, srcSpace, points);
        for (int i = 0; i < pts.Length; i++)
            pts[i] = Point.Round(points[i]);
    }

    private SKMatrix SpaceToDevice(CoordinateSpace space) => space switch
    {
        CoordinateSpace.World => PageMatrix.PreConcat(world),
        CoordinateSpace.Page => PageMatrix,
        _ => SKMatrix.Identity,
    };

    #endregion

    #region State

    public GraphicsState Save()
    {
        var state = new State
        {
            World = world,
            Clip = clip == null ? null : new SKPath(clip),
            PageUnit = pageUnit,
            PageScale = pageScale,
            PixelOffsetMode = pixelOffsetMode,
            SmoothingMode = SmoothingMode,
            TextRenderingHint = TextRenderingHint,
            InterpolationMode = InterpolationMode,
            CompositingQuality = CompositingQuality,
            CompositingMode = CompositingMode,
            TextContrast = TextContrast,
        };
        savedStates.Add(state);
        return new GraphicsState(state);
    }

    /// <summary>Restores a saved state and, as in GDI+, discards every state saved after it.</summary>
    public void Restore(GraphicsState gstate)
    {
        ArgumentNullException.ThrowIfNull(gstate);
        int index = savedStates.FindIndex(s => ReferenceEquals(s, gstate.Snapshot));
        if (index < 0)
            return;

        var state = savedStates[index];
        world = state.World;
        clip?.Dispose();
        clip = state.Clip == null ? null : new SKPath(state.Clip);
        pageUnit = state.PageUnit;
        pageScale = state.PageScale;
        pixelOffsetMode = state.PixelOffsetMode;
        SmoothingMode = state.SmoothingMode;
        TextRenderingHint = state.TextRenderingHint;
        InterpolationMode = state.InterpolationMode;
        CompositingQuality = state.CompositingQuality;
        CompositingMode = state.CompositingMode;
        TextContrast = state.TextContrast;

        for (int i = index; i < savedStates.Count; i++)
            savedStates[i].Clip?.Dispose();
        savedStates.RemoveRange(index, savedStates.Count - index);
        ApplyCanvasState();
    }

    #endregion

    #region Clip

    public void SetClip(Graphics g) => SetClip(g, CombineMode.Replace);

    public void SetClip(Graphics g, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(g);
        CombineClip(g.clip, combineMode);
    }

    public void SetClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Replace);

    public void SetClip(Rectangle rect, CombineMode combineMode) => SetClip((RectangleF)rect, combineMode);

    public void SetClip(RectangleF rect) => SetClip(rect, CombineMode.Replace);

    public void SetClip(RectangleF rect, CombineMode combineMode)
    {
        using var shape = Region.RectPath(rect);
        using var device = ToDevice(shape);
        CombineClip(device, combineMode);
    }

    public void SetClip(GraphicsPath path) => SetClip(path, CombineMode.Replace);

    public void SetClip(GraphicsPath path, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var device = ToDevice(path.path);
        CombineClip(device, combineMode);
    }

    public void SetClip(Region region, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region.path == null)
        {
            CombineClip(null, combineMode);
            return;
        }

        using var device = ToDevice(region.path);
        CombineClip(device, combineMode);
    }

    public void IntersectClip(RectangleF rect) => SetClip(rect, CombineMode.Intersect);

    public void IntersectClip(Rectangle rect) => SetClip(rect, CombineMode.Intersect);

    public void IntersectClip(Region region) => SetClip(region, CombineMode.Intersect);

    public void ExcludeClip(Rectangle rect) => SetClip(rect, CombineMode.Exclude);

    public void ExcludeClip(Region region) => SetClip(region, CombineMode.Exclude);

    public void ResetClip()
    {
        clip?.Dispose();
        clip = null;
        ApplyCanvasState();
    }

    public void TranslateClip(float dx, float dy)
    {
        if (clip == null)
            return;
        var offset = DeviceMatrix.MapVector(dx, dy);
        clip.Offset(offset.X, offset.Y);
        ApplyCanvasState();
    }

    public void TranslateClip(int dx, int dy) => TranslateClip((float)dx, dy);

    public bool IsVisible(RectangleF rect)
    {
        var bounds = rect.ToSKRect().Standardized;
        // Degenerate rectangles (e.g. horizontal lines) are still visible in GDI+.
        if (bounds.Width == 0 || bounds.Height == 0)
            bounds.Inflate(bounds.Width == 0 ? 1e-3f : 0, bounds.Height == 0 ? 1e-3f : 0);
        return !canvas.QuickReject(bounds);
    }

    public bool IsVisible(Rectangle rect) => IsVisible((RectangleF)rect);

    public bool IsVisible(float x, float y, float width, float height) => IsVisible(new RectangleF(x, y, width, height));

    public bool IsVisible(int x, int y, int width, int height) => IsVisible(new RectangleF(x, y, width, height));

    public bool IsVisible(PointF point) => IsVisible(new RectangleF(point, SizeF.Empty));

    public bool IsVisible(Point point) => IsVisible((PointF)point);

    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y));

    public bool IsVisible(int x, int y) => IsVisible(new PointF(x, y));

    private void CombineClip(SKPath? deviceShape, CombineMode mode)
    {
        var result = Region.Combine(clip, deviceShape, mode);
        clip?.Dispose();
        clip = result;
        ApplyCanvasState();
    }

    private SKPath ToDevice(SKPath worldPath)
    {
        var result = new SKPath(worldPath);
        result.Transform(DeviceMatrix);
        return result;
    }

    private SKPath ToWorld(SKPath devicePath)
    {
        var result = new SKPath(devicePath);
        if (DeviceMatrix.TryInvert(out var inverse))
            result.Transform(inverse);
        return result;
    }

    #endregion

    public Color GetNearestColor(Color color) => color;

    public void Flush() => canvas.Flush();

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        canvas.RestoreToCount(baseSaveCount);
        if (ownsCanvas)
            canvas.Dispose();

        clip?.Dispose();
        foreach (var state in savedStates)
            state.Clip?.Dispose();
        savedStates.Clear();
        target?.InvalidateCache();
    }

    private void ApplyMatrix() => canvas.SetMatrix(baseMatrix.PreConcat(DeviceMatrix));

    private void ApplyCanvasState()
    {
        canvas.RestoreToCount(baseSaveCount);
        canvas.Save();
        if (clip != null)
            canvas.ClipPath(clip, SKClipOperation.Intersect, false);
        ApplyMatrix();
    }

    /// <summary>Marks the target bitmap as modified so its cached drawing snapshot is refreshed.</summary>
    private void Touch() => target?.InvalidateCache();

    private SKPaint CreatePaint(Brush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);
        var paint = new SKPaint { IsAntialias = Antialias, Style = SKPaintStyle.Fill };
        brush.ApplyTo(paint, canvas.TotalMatrix);
        ApplyCompositing(paint);
        return paint;
    }

    private SKPaint CreatePaint(Pen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);
        var paint = new SKPaint { IsAntialias = Antialias };
        pen.ApplyTo(paint, canvas.TotalMatrix);
        ApplyCompositing(paint);
        return paint;
    }

    private void ApplyCompositing(SKPaint paint)
    {
        if (CompositingMode == CompositingMode.SourceCopy)
            paint.BlendMode = SKBlendMode.Src;
    }

    private void StrokePath(Pen pen, SKPath path)
    {
        using var paint = CreatePaint(pen);
        DrawGeometry(() => canvas.DrawPath(path, paint));
    }

    private void FillSKPath(Brush brush, SKPath path)
    {
        using var paint = CreatePaint(brush);
        DrawGeometry(() => canvas.DrawPath(path, paint));
    }

    /// <summary>
    /// Draws geometry, applying GDI+'s integer pixel-center convention to antialiased output
    /// (see <see cref="PixelOffsetMode"/>). Paints are created beforehand so device-aligned brushes
    /// keep using the unshifted device grid.
    /// </summary>
    private void DrawGeometry(Action draw)
    {
        Touch();
        if (!UsesHalfPixelOffset)
        {
            draw();
            return;
        }

        int saveCount = canvas.Save();
        canvas.SetMatrix(halfPixelShift.PreConcat(canvas.TotalMatrix));
        draw();
        canvas.RestoreToCount(saveCount);
    }

    private static SKPath BuildPath(Action<SKPathBuilder> build, SKPathFillType fillType = SKPathFillType.Winding)
    {
        using var builder = new SKPathBuilder { FillType = fillType };
        build(builder);
        return builder.Snapshot();
    }

    private static SKPathFillType ToFillType(FillMode mode) =>
        mode == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd;

    private sealed class State
    {
        public SKMatrix World;
        public SKPath? Clip;
        public GraphicsUnit PageUnit;
        public float PageScale;
        public PixelOffsetMode PixelOffsetMode;
        public SmoothingMode SmoothingMode;
        public TextRenderingHint TextRenderingHint;
        public InterpolationMode InterpolationMode;
        public CompositingQuality CompositingQuality;
        public CompositingMode CompositingMode;
        public int TextContrast;
    }
}
