using System.Drawing.Imaging;
using System.Text;
using SkiaSharp;
using Svg.Skia;

namespace System.Drawing;

/// <summary>
/// A vector image, held as recorded Skia drawing commands (<see cref="SKPicture"/>). SVG documents load as
/// vector images, through <see cref="Image.FromStream(Stream)"/> and <see cref="Image.FromFile(string)"/>.
/// </summary>
/// <remarks>
/// Drawing a vector image onto a <see cref="Graphics"/> replays its commands, so it stays sharp at any size and
/// PDF and SVG output keep it as vectors. Operations that need pixels, like brushes, saving to raster formats or
/// <see cref="Bitmap(Image)"/>, work on a rasterization at the intrinsic size (<see cref="Image.Width"/> ×
/// <see cref="Image.Height"/> at 96 DPI).
/// </remarks>
public sealed class VectorImage : Image
{
    private SKPicture picture;
    private SKRect bounds;
    private byte[]? svgData;
    private SKBitmap? raster;
    private SKImage? rasterImage;

    /// <summary>Creates a vector image from recorded drawing commands; the image takes ownership of the picture.</summary>
    /// <param name="picture">The drawing commands.</param>
    /// <param name="bounds">The area of the picture that makes up the image; by default its cull rectangle.</param>
    public VectorImage(SKPicture picture, SKRect? bounds = null)
    {
        ArgumentNullException.ThrowIfNull(picture);
        this.picture = picture;
        this.bounds = bounds ?? picture.CullRect;
        if (this.bounds.Width <= 0 || this.bounds.Height <= 0)
            throw new ArgumentException("The image has no size.");
    }

    /// <summary>Loads an SVG document.</summary>
    /// <exception cref="ArgumentException">The data is not a valid SVG document.</exception>
    public static VectorImage FromSvg(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var svg = new SKSvg();
        using (var stream = new MemoryStream(data, false))
        {
            if (svg.Load(stream) is null || svg.Picture is null)
                throw new ArgumentException("The SVG document could not be loaded.");
        }

        // The picture belongs to the SKSvg instance: keep a copy that references it.
        var cullRect = svg.Picture.CullRect;
        using var recorder = new SKPictureRecorder();
        recorder.BeginRecording(cullRect).DrawPicture(svg.Picture);
        return new VectorImage(recorder.EndRecording(), cullRect)
        {
            svgData = data,
            RawFormat = ImageFormat.Svg,
        };
    }

    /// <summary>Loads an SVG document.</summary>
    public static VectorImage FromSvg(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return FromSvg(memory.ToArray());
    }

    /// <summary>Returns whether the data looks like an SVG document (an XML document with an svg root element).</summary>
    public static bool IsSvg(byte[] data)
    {
        if (data == null || data.Length < 4)
            return false;
        // Enough to get past an XML declaration, comments and a DOCTYPE.
        string head = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 4096)).TrimStart('﻿', ' ', '\t', '\r', '\n');
        return head.StartsWith("<", StringComparison.Ordinal) && head.IndexOf("<svg", StringComparison.Ordinal) >= 0;
    }

    /// <summary>The drawing commands. The image owns them.</summary>
    public SKPicture Picture => picture;

    /// <summary>The area of <see cref="Picture"/> that makes up the image.</summary>
    public SKRect Bounds => bounds;

    /// <summary>The SVG document the image was loaded from, if any.</summary>
    public byte[]? SvgData => svgData;

    public override int Width => Math.Max(1, (int)Math.Ceiling(bounds.Width));

    public override int Height => Math.Max(1, (int)Math.Ceiling(bounds.Height));

    public override float HorizontalResolution => 96f;

    public override float VerticalResolution => 96f;

    public override PixelFormat PixelFormat => PixelFormat.Format32bppArgb;

    public override void RotateFlip(RotateFlipType rotateFlipType)
    {
        int rotation = (int)rotateFlipType & 3;
        bool flipX = ((int)rotateFlipType & 4) != 0;
        if (rotation == 0 && !flipX)
            return;

        float width = bounds.Width, height = bounds.Height;
        float newWidth = rotation % 2 == 1 ? height : width;
        float newHeight = rotation % 2 == 1 ? width : height;

        // Image pixel space, then rotation clockwise, then horizontal flip.
        var matrix = SKMatrix.CreateTranslation(-bounds.Left, -bounds.Top);
        matrix = SKMatrix.Concat(rotation switch
        {
            1 => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            2 => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            3 => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity,
        }, matrix);
        if (flipX)
            matrix = SKMatrix.Concat(new SKMatrix(-1, 0, newWidth, 0, 1, 0, 0, 0, 1), matrix);

        var newBounds = new SKRect(0, 0, newWidth, newHeight);
        using var recorder = new SKPictureRecorder();
        recorder.BeginRecording(newBounds).DrawPicture(picture, in matrix);
        ReplacePicture(recorder.EndRecording(), newBounds);
        // The SVG source no longer matches.
        svgData = null;
        RawFormat = ImageFormat.MemoryBmp;
    }

    public override object Clone()
    {
        // A new picture that references this one, so both can be disposed independently.
        using var recorder = new SKPictureRecorder();
        recorder.BeginRecording(bounds).DrawPicture(picture);
        return new VectorImage(recorder.EndRecording(), bounds) { svgData = svgData, RawFormat = RawFormat, Tag = Tag };
    }

    /// <summary>Draws the part <paramref name="source"/> (in image pixels) of the image into <paramref name="destination"/>.</summary>
    internal void Draw(SKCanvas canvas, SKRect source, SKRect destination, SKPaint? paint)
    {
        if (source.Width <= 0 || source.Height <= 0)
            return;

        int saveCount = canvas.Save();
        canvas.ClipRect(destination);
        var matrix = SKMatrix.CreateTranslation(destination.Left, destination.Top);
        matrix = SKMatrix.Concat(matrix, SKMatrix.CreateScale(destination.Width / source.Width, destination.Height / source.Height));
        matrix = SKMatrix.Concat(matrix, SKMatrix.CreateTranslation(-source.Left - bounds.Left, -source.Top - bounds.Top));
        canvas.DrawPicture(picture, in matrix, paint);
        canvas.RestoreToCount(saveCount);
    }

    internal override SKImage GetSKImage() => rasterImage ??= SKImage.FromBitmap(GetSKBitmap());

    internal override SKBitmap GetSKBitmap()
    {
        if (raster == null)
        {
            raster = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(raster);
            canvas.Clear(SKColors.Transparent);
            Draw(canvas, new SKRect(0, 0, bounds.Width, bounds.Height), new SKRect(0, 0, bounds.Width, bounds.Height), null);
        }
        return raster;
    }

    /// <summary>Writes the SVG source when saved as <see cref="ImageFormat.Svg"/>.</summary>
    internal bool TrySaveSvg(Stream stream, ImageFormat format)
    {
        if (svgData == null || !format.Equals(ImageFormat.Svg))
            return false;
        stream.Write(svgData, 0, svgData.Length);
        return true;
    }

    private void ReplacePicture(SKPicture newPicture, SKRect newBounds)
    {
        picture.Dispose();
        picture = newPicture;
        bounds = newBounds;
        DisposeRaster();
    }

    private void DisposeRaster()
    {
        rasterImage?.Dispose();
        rasterImage = null;
        raster?.Dispose();
        raster = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeRaster();
            picture.Dispose();
        }
        base.Dispose(disposing);
    }
}
